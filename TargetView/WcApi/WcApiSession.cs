using HarmonyLib;
using Sandbox.Game.World;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;

namespace TargetView.WcApi;

[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
internal class WcApiSession : MySessionComponentBase
{
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

    public static Vector3D? GetPainterPos()
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
            long entityId = Traverse.Create(paintedTarget).Field("EntityId").GetValue<long>();
            Vector3D worldPos = Traverse.Create(paintedTarget).Field("FakeInfo").Field("WorldPosition").GetValue<Vector3D>();

            if (entityId != 0)
            {
                return worldPos;
            }
        }

        return null;
    }
}
