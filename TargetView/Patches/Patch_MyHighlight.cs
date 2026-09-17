using HarmonyLib;
using System;
using VRageRender;

namespace TargetView.Patches;

[HarmonyPatch]
public static class Patch_MyHighlight
{
    [HarmonyPatch(typeof(MyHighlight), nameof(MyHighlight.CopyDepthStencil))]
    [HarmonyPrefix]
    public static bool CopyDepthStencil_Prefix()
    {
        return !TargetViewRenderer.IsDrawing;
    }

    [HarmonyPatch(typeof(MyHighlight), nameof(MyHighlight.Run))]
    [HarmonyPrefix]
    public static bool Run_Prefix()
    {
        return !TargetViewRenderer.IsDrawing;
    }
}
