using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace TargetView.WcApi;

[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
internal class WcApiSession : MySessionComponentBase
{
    public static class Materials
    {
        public static readonly MyStringId TargetReticle = MyStringId.GetOrCompute("TargetReticle");
        public static readonly MyStringId BlockTargetAtlas = MyStringId.GetOrCompute("BlockTargetAtlas");
    }

    const string WC_SCRIPT_NAMESPACE = "CoreSystems";

    public static bool WcPresent => _apiReady || _wcSessionType != null;

    private static CoreSystems.Api.WcApi? _api;
    private static bool _apiReady;

    private static Type? _wcSessionType;

    public override void LoadData()
    {
        _api = new CoreSystems.Api.WcApi();
        _api.Load(() => _apiReady = true, false);

        Assembly? wcModAssembly = MyScriptManager.Static.Scripts.FirstOrDefault(i => i.Key.ToString().Contains(WC_SCRIPT_NAMESPACE)).Value;
        if (wcModAssembly is not null)
        {
            _wcSessionType = wcModAssembly.GetType("CoreSystems.Session");
        }
    }

    protected override void UnloadData()
    {
        _api?.Unload();
        _api = null;
        _apiReady = false;
        _wcSessionType = null;
    }

    public static MyEntity? GetLockedTarget(MyEntity? shooter)
    {
        if (shooter is null || _api is null || !_apiReady)
        {
            return null;
        }

        var target = _api.GetAiFocus(shooter);
        if (target is null || !_api.IsInRange(shooter).Item1)
            return null;
        return target;
    }

    private static object? TryGetPaintedTarget()
    {
        if (_api is null || !_apiReady || _wcSessionType == null)
            return null;

        // get CoreSystems.Session.I.PlayerDummyTargets (Dictionary<Long, FakeTargets>)
        // cast to generic dictionary since the value type is defined in the mod
        IDictionary? playerDummyTargets = Traverse.Create(_wcSessionType).Field("I").Field("PlayerDummyTargets").GetValue<IDictionary>();

        long playerId = MySession.Static.LocalPlayerId;
        if (playerDummyTargets != null && playerDummyTargets.Contains(playerId))
        {
            // reference: https://github.com/Ash-LikeSnow/WeaponCore/blob/e4d01b9a4150974fc1bac7eec0c840aae73b28f3/Data/Scripts/CoreSystems/Ai/AiTypes.cs#L155-L182
            object paintedTarget = Traverse.Create(playerDummyTargets[playerId]).Field("PaintedTarget").GetValue();
            return paintedTarget;
        }
        return null;
    }

    private static uint GetWcSessionTick()
    {
        if (_api is null || !_apiReady || _wcSessionType == null)
            return 0;

        return Traverse.Create(_wcSessionType).Field("I").Field("Tick").GetValue<uint>();
    }

    public static Vector3D? GetPainterPos()
    {
        if (TryGetPaintedTarget() is object paintedTarget)
        {
            long entityId = Traverse.Create(paintedTarget).Field("EntityId").GetValue<long>();
            Vector3D localPos = Traverse.Create(paintedTarget).Field("LocalPosition").GetValue<Vector3D>();

            if (entityId != 0 && MyEntities.GetEntityById(entityId)?.GetTopMostParent(typeof(MyCubeGrid)) is MyCubeGrid paintedGrid && paintedGrid.PositionComp is not null)
            {
                return Vector3D.Transform(localPos, paintedGrid.PositionComp.WorldMatrixRef);
            }
        }

        return null;
    }

    // Vector3D hitPos, uint tick, MyEntity ent = null, long entId = 0
    private static readonly Type[] _painterUpdateParamTypes =
    {
        typeof(Vector3D), typeof(uint), typeof(MyEntity), typeof(long)
    };
    private static readonly object[] _painterUpdateArgs = new object[4];

    public static void SetPainterPos(Vector3D worldPos, MyEntity targetEntity)
    {
        if (TryGetPaintedTarget() is object paintedTarget)
        {
            _painterUpdateArgs[0] = (Vector3D)worldPos;
            _painterUpdateArgs[1] = (uint)GetWcSessionTick();
            _painterUpdateArgs[2] = (MyEntity)targetEntity;
            _painterUpdateArgs[3] = (long)0;
            Traverse.Create(paintedTarget).Method("Update", _painterUpdateParamTypes, _painterUpdateArgs).GetValue();
        }
    }
}
