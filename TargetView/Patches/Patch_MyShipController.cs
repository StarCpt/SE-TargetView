using HarmonyLib;
using Sandbox.Game.Entities;
using VRageMath;

namespace TargetView.Patches;

[HarmonyPatch]
public static class Patch_MyShipController
{
    [HarmonyPatch(typeof(MyShipController), nameof(MyShipController.MoveAndRotate), [ typeof(Vector3), typeof(Vector2), typeof(float) ])]
    [HarmonyPrefix]
    public static void MoveAndRotate_Prefix(MyShipController __instance, ref Vector2 rotationIndicator)
    {
        if (TargetViewManager.IsPainting && __instance.EntityId == TargetViewManager.ControlledCockpitId)
        {
            rotationIndicator.X = 0;
            rotationIndicator.Y = 0;
        }
    }
}
