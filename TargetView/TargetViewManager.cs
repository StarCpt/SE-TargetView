using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.ModAPI.Physics;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using TargetView.Patches;
using TargetView.WcApi;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.Utils;
using VRage.Input;
using VRage.Render.Scene;
using VRage.Render11.Common;
using VRage.Render11.Resources;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace TargetView;

public static class TargetViewManager
{
    private struct RendererState
    {
        public bool Lodding;
        public bool DrawBillboards;
        public bool EyeAdaption;
        public bool Flares;
        public bool SSAO;
        public bool Bloom;
        public bool ShadowCameraFrozen;
        public Vector2I ViewportResolution;
        public Vector2I ResolutionI;

        private static readonly RendererState _cameraViewState = new()
        {
            Lodding = false,
            DrawBillboards = true,
            EyeAdaption = true, // when turned off, makes the image too bright when surface is lit by sunlight
            Flares = false,
            SSAO = false,
            Bloom = false,
            ShadowCameraFrozen = true, // don't update shadow camera as it causes flickering for distant shadows
            //ViewportResolution = ,
            //ResolutionI = ,
        };

        public static RendererState GetCameraViewState(Vector2I surfaceResolution)
        {
            return _cameraViewState with
            {
                ViewportResolution = surfaceResolution,
                ResolutionI = surfaceResolution,
            };
        }
    }

    private struct CameraState
    {
        public MatrixD ViewMatrix;
        public MatrixD ProjMatrix;
        public MatrixD ProjFarMatrix;
        public float Fov;
        public float NearPlane;
        public float FarPlane;
        public float ProjOffsetX;
        public float ProjOffsetY;
        public Vector3D CameraPos;

        public static CameraState From(MyEnvironmentMatrices matrices)
        {
            return new CameraState
            {
                ViewMatrix = matrices.ViewD,
                ProjMatrix = matrices.OriginalProjection,
                ProjFarMatrix = matrices.OriginalProjectionFar,
                Fov = matrices.FovH,
                NearPlane = matrices.NearClipping,
                FarPlane = matrices.FarClipping,
                ProjOffsetX = matrices.Projection.M31,
                ProjOffsetY = matrices.Projection.M32,
                CameraPos = matrices.CameraPosition,
            };
        }
    }

    private struct ControlledEntityData
    {
        public readonly double Radius => LocalVolume.Radius;

        public uint ActorId;
        public uint CockpitActorId;
        public BoundingSphereD LocalVolume; // center is relative to grid pivot
    }

    private struct TargetData
    {
        public readonly double Radius => LocalVolume.Radius;

        public uint ActorId;
        public BoundingSphereD LocalVolume;
        public Vector3D? LocalPainterPos;
    }

    private static TargetViewSettings Settings => Plugin.Settings;

    private static ControlledEntityData? _controlled = null;
    private static TargetData? _target = null;
    private static readonly object _sync = new();

    private static bool _zoom = false;
    private static float _zoomAmount = 0; // 0 = no zoom, 1 = full zoom

    private static MyCubeGrid? _controlledGrid;
    private static MyCubeGrid? _targetGrid;

    /// <summary>
    /// Main thread only
    /// </summary>
    public static void Update()
    {
        MyCockpit? controlledCockpit = MySession.Static.LocalCharacter?.Parent as MyCockpit;
        MyCubeGrid? controlledGrid = controlledCockpit?.CubeGrid;

        MyCubeGrid? target = null;
        Vector3D? targetPaintPosLocal = null;

        if (WcApiSession.WcPresent)
        {
            target = WcApiSession.GetLockedTarget(controlledCockpit)?.GetTopMostParent(typeof(MyCubeGrid)) as MyCubeGrid;
            targetPaintPosLocal = WcApiSession.GetLocalPainterPos();
        }
        else if (controlledCockpit?.TargetData is { IsTargetLocked: true} targetData &&
            MyEntities.TryGetEntityById(targetData.TargetId, out var lockedEntity, false) &&
            controlledGrid?.Components.Get<MyGridTargeting>() is MyGridTargeting gridTargeting &&
            gridTargeting.IsTargetLocked(lockedEntity))
        {
            target = lockedEntity.GetTopMostParent(typeof(MyCubeGrid)) as MyCubeGrid;
        }

        bool newZoom = MyInput.Static.IsKeyPress(Plugin.Settings.ZoomKey);

        lock (_sync)
        {
            if (controlledCockpit is null || controlledGrid?.PositionComp is null)
            {
                _controlled = null;
                _controlledGrid = null;
            }
            else
            {
                _controlled = new ControlledEntityData
                {
                    ActorId = controlledGrid.Render?.GetRenderObjectID() ?? uint.MaxValue,
                    CockpitActorId = controlledCockpit.Render?.GetRenderObjectID() ?? uint.MaxValue,
                    LocalVolume = controlledGrid.PositionComp.LocalVolume,
                };
                _controlledGrid = controlledGrid;
            }

            if (target?.PositionComp is null)
            {
                _target = null;
                _targetGrid = null;
            }
            else
            {
                _target = new TargetData
                {
                    ActorId = target.Render?.GetRenderObjectID() ?? uint.MaxValue,
                    LocalVolume = target.PositionComp.LocalVolume,
                    LocalPainterPos = targetPaintPosLocal,
                };
                _targetGrid = target;
            }

            if (newZoom != _zoom)
            {
                _zoomAmount = _zoom ? Utils.CubicEaseOut(_zoomAmount) : Utils.CubicEaseIn(_zoomAmount);
                _zoom = newZoom;
            }
        }
    }

    public static bool IsPainting { get; private set; } = false;
    private static Vector2 _paintCursorUV = new Vector2(0.5f); // [0,1]

    // render -> game
    private static MyViewport _lastViewport = default;
    private static MatrixD _lastViewMatrix = MatrixD.Identity;
    private static MatrixD _lastProjMatrix = MatrixD.Identity;

    public static void HandleInput()
    {
        if (!WcApiSession.WcPresent)
            return;

        if (!_controlled.HasValue || !_target.HasValue || _controlledGrid is null || _targetGrid is null)
        {
            IsPainting = false;
            return;
        }

        IsPainting = Plugin.Settings.PainterKey != MyKeys.None && MyInput.Static.IsKeyPress(Plugin.Settings.PainterKey);
        if (IsPainting && MyTransparentMaterials.TryGetMaterial(WcApiSession.Materials.TargetReticle, out var targetReticleMaterial))
        {
            // draw cursor
            MyViewport targetViewport = _lastViewport;

            Vector2 mouseDelta = MyInputExtensions.GetCursorPositionDelta(MyInput.Static) / new Vector2(targetViewport.Width, targetViewport.Height);
            _paintCursorUV = _paintCursorUV.IsValid() ? Vector2.Clamp(_paintCursorUV + mouseDelta, Vector2.Zero, Vector2.One) : Vector2.Zero;

            Rectangle screenRect = MyGuiManager.GetFullscreenRectangle();
            Vector2I screenSize = new Vector2I(screenRect.Width, screenRect.Height);

            Vector2 topLeft = new Vector2(targetViewport.OffsetX / screenSize.X, targetViewport.OffsetY / screenSize.Y);
            Vector2 bottomRight = topLeft + new Vector2(targetViewport.Width / screenSize.X, targetViewport.Height / screenSize.Y);

            Vector2 screenUV = new Vector2
            {
                X = MathHelper.Lerp(topLeft.X, bottomRight.X, _paintCursorUV.X),
                Y = MathHelper.Lerp(topLeft.Y, bottomRight.Y, _paintCursorUV.Y),
            };

            Utils.DrawMouseCursor(targetReticleMaterial.Texture, screenUV, 32);

            if (MyInput.Static.IsNewLeftMousePressed() && _controlledGrid.PositionComp != null)
            {
                Vector3D cameraPos = _controlledGrid.PositionComp.WorldAABB.Center;
                double cameraNearplane = _controlled.Value.Radius;

                RayD ray = default;
                ray.Direction = Utils.ComputeWorldRay(_paintCursorUV, _lastViewMatrix, _lastProjMatrix);
                ray.Position = cameraPos + -_lastViewMatrix.Col2 * cameraNearplane;

                if (_targetGrid.PositionComp.WorldAABB.Intersect(ref ray, out double aabbHitNear, out double aabbHitFar))
                {
                    // fire ray from aabb hit
                    Vector3D rayFrom = ray.Position + ray.Direction * aabbHitNear;
                    Vector3D rayTo = ray.Position + ray.Direction * aabbHitFar;

                    if (((IMyPhysics)MyPhysics.Static).CastRay(rayFrom, rayTo, out var hitInfo) &&
                        hitInfo.HitEntity.GetTopMostParent(typeof(MyCubeGrid)) as MyCubeGrid == _targetGrid)
                    {
                        WcApiSession.SetPainterPos(hitInfo.Position, _targetGrid);
                    }
                }
            }
        }
    }

    private static readonly MyBillboard _paintIconBillboard = new()
    {
        BlendType = MyBillboard.BlendTypeEnum.PostPP,
    };

    private static void UpdatePaintIconBillboard(Vector3D? painterPos, Vector3D targetPos, Vector3D cameraPos, MatrixD viewMatrix, MatrixD projMatrix, Vector2 viewportSize, double fov)
    {
        if (!painterPos.HasValue)
            return;

        if (!MyTransparentMaterials.TryGetMaterial(WcApiSession.Materials.BlockTargetAtlas, out var mat))
            return;

        Vector3D targetDir = Vector3D.Normalize(targetPos - cameraPos);
        MatrixD viewProjMatrix = viewMatrix * projMatrix;

        float aspectRatio = viewportSize.X / viewportSize.Y;
        double fovDistScale = Math.Tan(fov * 0.5) * 0.1;

        Vector3D screenPos = Vector3D.Transform(painterPos.Value, viewProjMatrix);

        // align to pixel
        //screenPos.X = Math.Round(screenPos.X * viewportSize.X * 0.5) / (viewportSize.X * 0.5);
        //screenPos.Y = Math.Round(screenPos.Y * viewportSize.Y * 0.5) / (viewportSize.Y * 0.5);

        screenPos.X *= fovDistScale * aspectRatio;
        screenPos.Y *= fovDistScale;
        screenPos.Z = -0.1;

        Vector3 billboardLeft = -viewMatrix.Col0;
        Vector3 billboardUp = viewMatrix.Col1;

        Vector3D billboardWorldPos = Vector3D.Transform(screenPos, MatrixD.CreateWorld(cameraPos, targetDir, (Vector3D)billboardUp));

        int offsetIndex = MySession.Static.GameplayFrameCounter % 20;
        offsetIndex = offsetIndex < 10 ? offsetIndex : 19 - offsetIndex;

        Vector2 uvOffset = new Vector2(0, offsetIndex * 0.1f);
        Vector2 uvSize = new Vector2(1, 0.1f);

        Vector4 color = new Vector4(0.025f, 1, 0.25f, 2) * 1.25f;

        float sizeInPx = 32;
        float screenSize = sizeInPx / Math.Max(viewportSize.X, viewportSize.Y) * (float)fovDistScale;

        MyUtils.GetBillboardQuadOriented(out MyQuadD quad, ref billboardWorldPos, screenSize, screenSize, ref billboardLeft, ref billboardUp);
        MyTransparentGeometry.CreateBillboard(_paintIconBillboard, ref quad, mat.Id, ref color, ref billboardWorldPos);
        _paintIconBillboard.UVOffset = uvOffset;
        _paintIconBillboard.UVSize = uvSize;
    }

    /// <summary>
    /// Render thread only
    /// </summary>
    /// <returns></returns>
    public static bool Draw(Format rtvFormat, out IBorrowedRtvTexture targetViewTexture, out MyViewport targetViewViewport)
    {
        targetViewTexture = null!;
        targetViewViewport = default;

        if (MySession.Static is null || !MySession.Static.Ready)
            return false;

        MyCamera renderCamera = MySector.MainCamera;
        if (renderCamera is null)
            return false;

        ControlledEntityData controlledEntity;
        TargetData target;
        float zoomAmount;
        
        lock (_sync)
        {
            if (!_controlled.HasValue || !_target.HasValue)
            {
                return false;
            }
            else
            {
                controlledEntity = _controlled.Value;
                target = _target.Value;
            }

            float zoomDelta = MyCommon.GetLastFrameDelta() * (_zoom ? 1 : -1) * ((float)Settings.ZoomSpeed * 1.65f);
            _zoomAmount = MathHelper.Clamp(_zoomAmount + zoomDelta, 0, 1);
            zoomAmount = _zoomAmount;
        }

        Vector3D controlledEntityPos;
        Vector3D cameraUp;

        Vector3D targetPos;
        Vector3D? targetPainterPos = null;

        if (!TryGetActor(controlledEntity.ActorId, out MyActor controlledGridActor)
            || !TryGetActor(controlledEntity.CockpitActorId, out MyActor controlledCockpitActor)
            || !TryGetActor(target.ActorId, out MyActor targetGridActor))
        {
            return false;
        }
        else
        {
            controlledEntityPos = Vector3D.Transform(controlledEntity.LocalVolume.Center, controlledGridActor.WorldMatrix);
            cameraUp = controlledCockpitActor.WorldMatrix.Up;

            targetPos = Vector3D.Transform(target.LocalVolume.Center, targetGridActor.WorldMatrix);
            if (target.LocalPainterPos.HasValue)
            {
                targetPainterPos = Vector3D.Transform(target.LocalPainterPos.Value, targetGridActor.WorldMatrix);
            }
        }

        var originalRendererState = new RendererState
        {
            Lodding = MyCommon.LoddingSettings.Global.IsUpdateEnabled,
            DrawBillboards = MyRender11.Settings.DrawBillboards,
            EyeAdaption = MyRender11.Postprocess.EnableEyeAdaptation,
            Flares = MyRender11.DebugOverrides.Flares,
            SSAO = MyRender11.DebugOverrides.SSAO,
            Bloom = MyRender11.DebugOverrides.Bloom,
            ShadowCameraFrozen = MyRender11.Settings.ShadowCameraFrozen,
            ViewportResolution = MyRender11.ViewportResolution,
            ResolutionI = MyRender11.ResolutionI,
        };
        var originalCameraState = CameraState.From(MyRender11.Environment.Matrices);

        Vector3D targetDir = Vector3D.Normalize(targetPos - controlledEntityPos);
        Vector3D cameraPos = controlledEntityPos + (targetDir * controlledEntity.Radius);

        double targetDist = Vector3D.Distance(targetPos, controlledEntityPos);

        if (targetDist < Math.Max(Settings.MinDistance, controlledEntity.Radius + target.Radius) || targetDist > renderCamera.FarPlaneDistance)
            return false;

        Vector2I backbufferRes = MyRender11.BackBufferResolution;

        Vector2I zoomedPos = new Vector2I(20);
        Vector2I zoomedSize = new Vector2I(backbufferRes - (zoomedPos * 2));

        float smoothZoomAmount = MathHelper.Saturate(_zoom ? Utils.CubicEaseOut(zoomAmount) : Utils.CubicEaseIn(zoomAmount));
        Vector2I viewportPos = Utils.Lerp(Settings.Position, zoomedPos, smoothZoomAmount);
        Vector2I viewportRes = Utils.Lerp(Settings.Size, zoomedSize, smoothZoomAmount);

        bool invalidPos = viewportPos.X >= backbufferRes.X || viewportPos.Y >= backbufferRes.Y;
        bool invalidRes = viewportRes.X < 20 || viewportRes.Y < 20 || viewportRes.X > backbufferRes.X || viewportRes.Y > backbufferRes.Y; // game may crash when viewport is too small

        if (invalidPos || invalidRes)
            return false;

        double aspectRatio = (double)viewportRes.X / (double)viewportRes.Y;
        double fov = 2 * Math.Atan2(target.Radius / MathHelper.Saturate(aspectRatio), targetDist);

        // this "safe fov" thing is wrong but it works
        double eps = 0.0000001;
        double safeFovV = MathHelper.Clamp(2 * Math.Atan2(target.Radius, targetDist) * aspectRatio, eps, Math.PI - eps);
        MatrixD projMatrix = MatrixD.CreatePerspectiveFieldOfView(safeFovV, aspectRatio, renderCamera.NearPlaneDistance, renderCamera.FarPlaneDistance);
        MatrixD viewMatrix = MatrixD.CreateLookAt(cameraPos, targetPos, cameraUp);

        UpdatePaintIconBillboard(targetPainterPos, targetPos, cameraPos, viewMatrix, projMatrix, viewportRes, safeFovV);

        _lastViewport = new MyViewport(viewportPos.X, viewportPos.Y, viewportRes.X, viewportRes.Y);
        _lastViewMatrix = viewMatrix;
        _lastProjMatrix = MatrixD.CreatePerspectiveFieldOfView(fov, aspectRatio, renderCamera.NearPlaneDistance, renderCamera.FarPlaneDistance);

        int paintIconBillboardIndex = -1;

        if (targetPainterPos.HasValue)
        {
            paintIconBillboardIndex = MyRenderProxy.BillboardsRead.Count;
            MyRenderProxy.BillboardsRead.Add(_paintIconBillboard);
        }

        {
            var tempRtv = MyManagers.RwTexturesPool.BorrowRtv("TargetViewRtv", backbufferRes.X, backbufferRes.Y, rtvFormat);

            // set state for CameraLCD rendering
            SetRendererState(RendererState.GetCameraViewState(viewportRes));
            SetCameraViewMatrix(originalCameraState with
            {
                ViewMatrix = viewMatrix,
                Fov = (float)fov,
                NearPlane = renderCamera.NearPlaneDistance,
                FarPlane = renderCamera.FarPlaneDistance,
                CameraPos = cameraPos,
                ProjOffsetX = 0,
                ProjOffsetY = 0,
            }, renderCamera.FarFarPlaneDistance, 1, false);

            TargetViewRenderer.Draw(tempRtv, true);

            // restore camera settings
            SetRendererState(originalRendererState);
            SetCameraViewMatrix(originalCameraState, renderCamera.FarFarPlaneDistance, 0, false);

            targetViewTexture = tempRtv;
            targetViewViewport = new MyViewport(viewportPos.X, viewportPos.Y, viewportRes.X, viewportRes.Y);
        }

        if (paintIconBillboardIndex != -1)
        {
            MyRenderProxy.BillboardsRead.RemoveAtFast(paintIconBillboardIndex);
        }

        return true;
    }

    private static void SetRendererState(RendererState state)
    {
        SetLoddingEnabled(state.Lodding);
        MyRender11.Settings.DrawBillboards = state.DrawBillboards;
        MyRender11.Postprocess.EnableEyeAdaptation = state.EyeAdaption;
        MyRender11.DebugOverrides.Flares = state.Flares;
        MyRender11.DebugOverrides.SSAO = state.SSAO;
        MyRender11.DebugOverrides.Bloom = state.Bloom;
        MyRender11.Settings.ShadowCameraFrozen = state.ShadowCameraFrozen;

        MyRender11.ViewportResolution = state.ViewportResolution;
        MyRender11.m_resolution = state.ResolutionI;

        static bool SetLoddingEnabled(bool enabled)
        {
            // Reference: MyRender11.ProcessMessageInternal(MyRenderMessageBase message, int frameId)
            //              case MyRenderMessageEnum.UpdateNewLoddingSettings

            MyNewLoddingSettings loddingSettings = MyCommon.LoddingSettings;
            MyGlobalLoddingSettings globalSettings = loddingSettings.Global;
            bool initial = globalSettings.IsUpdateEnabled;
            if (initial == enabled)
                return initial;

            globalSettings.IsUpdateEnabled = enabled;
            loddingSettings.Global = globalSettings;
            MyManagers.GeometryRenderer.IsLodUpdateEnabled = enabled;
            MyManagers.GeometryRenderer.m_globalLoddingSettings = globalSettings;
            MyManagers.ModelFactory.OnLoddingSettingChanged();
            return initial;
        }
    }

    private static void SetCameraViewMatrix(CameraState state, float farFarPlane, int lastMomentUpdateIndex, bool smooth)
    {
        MyRenderMessageSetCameraViewMatrix? renderMessage = null;
        try
        {
            renderMessage = MyRenderProxy.MessagePool.Get<MyRenderMessageSetCameraViewMatrix>(MyRenderMessageEnum.SetCameraViewMatrix);
            renderMessage.ViewMatrix = state.ViewMatrix;
            renderMessage.ProjectionMatrix = state.ProjMatrix;
            renderMessage.ProjectionFarMatrix = state.ProjFarMatrix;
            renderMessage.FOV = state.Fov;
            renderMessage.FOVForSkybox = state.Fov;
            renderMessage.NearPlane = state.NearPlane;
            renderMessage.FarPlane = state.FarPlane;
            renderMessage.FarFarPlane = farFarPlane;
            renderMessage.CameraPosition = state.CameraPos;
            renderMessage.LastMomentUpdateIndex = lastMomentUpdateIndex;
            renderMessage.ProjectionOffsetX = state.ProjOffsetX;
            renderMessage.ProjectionOffsetY = state.ProjOffsetY;
            renderMessage.Smooth = smooth;
            MyRender11.SetupCameraMatrices(renderMessage);
        }
        finally
        {
            renderMessage?.Dispose();
        }
    }

    private static bool TryGetActor(uint actorId, out MyActor actor)
    {
        try
        {
            actor = actorId != uint.MaxValue ? MyIDTracker<MyActor>.FindByID(actorId) : null!;
            return actor != null;
        }
        catch
        {
            actor = null!;
            return false;
        }
    }

}
