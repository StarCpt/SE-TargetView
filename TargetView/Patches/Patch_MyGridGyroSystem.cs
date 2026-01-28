using HarmonyLib;
using Sandbox.Game.GameSystems;
using VRageMath;

namespace TargetView.Patches;

[HarmonyPatch]
public static class Patch_MyGridGyroSystem
{
    [HarmonyPatch(typeof(MyGridGyroSystem), "Update")]
    [HarmonyPrefix]
    public static void Update_Prefix(ref Vector3 ___m_controlTorque)
    {
        if (TargetViewManager.IsPainting)
        {
            ___m_controlTorque.X = 0;
            ___m_controlTorque.Y = 0;
        }
    }
}
