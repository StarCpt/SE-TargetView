using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SharpDX.DXGI;
using SpaceEngineers.Game;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TargetView.Patches;
using TargetView.WcApi;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.Utils;
using VRage.Render.Scene;
using VRage.Render11.Common;
using VRage.Render11.Resources;
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
        public uint ControlledCockpitRenderId;
        public BoundingSphereD BoundingSphere;
        public Vector3D UpVector;
    }

    private struct TargetData
    {
        public BoundingSphereD BoundingSphere;
    }

    private static ControlledEntityData? _controlled = null;
    private static TargetData? _target = null;
    private static readonly object _sync = new();

    /// <summary>
    /// Main thread only
    /// </summary>
    public static void Update()
    {
        MyCockpit? controlledCockpit = MySession.Static.LocalCharacter?.Parent as MyCockpit;
        MyCubeGrid? controlledGrid = controlledCockpit?.CubeGrid;

        MyCubeGrid? target = WcApiSession.GetLockedTarget(controlledCockpit)?.GetTopMostParent(typeof(MyCubeGrid)) as MyCubeGrid;

        lock (_sync)
        {
            if (controlledCockpit is null || controlledGrid?.PositionComp is null)
            {
                _controlled = null;
            }
            else
            {
                _controlled = new ControlledEntityData
                {
                    ControlledCockpitRenderId = controlledCockpit.Render?.GetRenderObjectID() ?? uint.MaxValue,
                    BoundingSphere = controlledGrid.PositionComp.WorldVolume with { Center = controlledGrid.PositionComp.WorldAABB.Center },
                    UpVector = controlledGrid.PositionComp.WorldMatrixRef.Up,
                };
            }

            if (target?.PositionComp is null)
            {
                _target = null;
            }
            else
            {
                _target = new TargetData
                {
                    BoundingSphere = target.PositionComp.WorldVolume,
                };
            }
        }
    }

    private static Stopwatch _sw = new();

    /// <summary>
    /// Render thread only
    /// </summary>
    /// <returns></returns>
    public static bool Draw()
    {
        MyCamera renderCamera = MySector.MainCamera;
        if (renderCamera is null)
            return false;

        ControlledEntityData controlledEntity;
        TargetData target;
        
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

        Vector3D targetPos = target.BoundingSphere.Center;
        Vector3D targetDir = Vector3D.Normalize(targetPos - controlledEntity.BoundingSphere.Center);
        Vector3D cameraPos = controlledEntity.BoundingSphere.Center + (targetDir * controlledEntity.BoundingSphere.Radius);

        double targetDist = Vector3D.Distance(targetPos, cameraPos);
        double fov = 2 * Math.Asin(target.BoundingSphere.Radius / targetDist);

        {
            var tempRtv = MyManagers.RwTexturesPool.BorrowRtv("TargetViewRtv", 500, 500, Format.R16G16B16A16_Float);

            // set state for CameraLCD rendering
            SetRendererState(RendererState.GetCameraViewState(tempRtv.Size));
            GetViewMatrixAndPosition(cameraPos, controlledEntity.UpVector, targetPos, out MatrixD cameraViewMatrix);
            SetCameraViewMatrix(originalCameraState with
            {
                ViewMatrix = cameraViewMatrix,
                Fov = (float)fov,
                NearPlane = renderCamera.NearPlaneDistance,
                FarPlane = renderCamera.FarPlaneDistance,
                CameraPos = cameraPos,
                ProjOffsetX = 0,
                ProjOffsetY = 0,
            }, renderCamera.FarFarPlaneDistance, 1, false);

            TargetViewRenderer.Draw(tempRtv);
            MyRender11.RC.SetRtvNull();

            // restore camera settings
            SetRendererState(originalRendererState);
            SetCameraViewMatrix(originalCameraState, renderCamera.FarFarPlaneDistance, 0, false);

            Patch_MyRender11.TargetViewTexture = tempRtv;
        }

        return true;
    }

    private static void GetViewMatrixAndPosition(Vector3D cameraPos, Vector3D cameraUp, Vector3D targetPos, out MatrixD viewMatrix)
    {
        viewMatrix = MatrixD.CreateLookAt(cameraPos, targetPos, cameraUp);

        //throw new NotImplementedException();
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

    private static bool TryGetActor(MyEntity entity, out MyActor actor)
    {
        actor = null;
        if (entity?.Render is not MyRenderComponentBase renderComp)
        {
            return false;
        }

        try
        {
            uint actorId = renderComp.GetRenderObjectID();
            actor = actorId != uint.MaxValue ? MyIDTracker<MyActor>.FindByID(actorId) : null;
            return actor != null;
        }
        catch
        {
            return false;
        }
    }

}
