using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using VRage.Render11.RenderContext;
using VRageRender;

namespace TargetView.Patches;

[HarmonyPatch]
public static class Patch_MyShadowCascades
{
    [HarmonyPatch(typeof(MyShadowCascades), nameof(MyShadowCascades.PrepareQueries))]
    [HarmonyPrefix]
    public static bool PrepareQueries_Prefix(MyShadowCascades __instance, MyRenderContext rc, List<MyShadowmapQuery> appendShadowmapQueries)
    {
        if (!__instance.Enabled)
            return true;

        if (TargetViewRenderer.IsDrawing)
        {
            __instance.m_cascadeStats.Update();
            __instance.FillConstantBuffer(rc, __instance.m_csmConstants2);
            __instance.FillConstantBuffer(rc, __instance.m_csmConstants);
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(MyShadowCascades), nameof(MyShadowCascades.Gather))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Gather_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        // insert this code at the beginning of the method:
        // if (TargetViewRenderer.IsDrawing)
        // {
        //     return;
        // }

        List<CodeInstruction> patchedMethod = [];

        Label labelToOriginalMethod = generator.DefineLabel();

        MethodInfo isTargetViewDrawingGetter = AccessTools.PropertyGetter(typeof(TargetViewRenderer), nameof(TargetViewRenderer.IsDrawing));
        // call bool TargetViewRenderer.IsDrawing getter, result is at the top of the stack
        patchedMethod.Add(new CodeInstruction(OpCodes.Call, isTargetViewDrawingGetter));
        // jump to original method if !TargetViewRenderer.IsDrawing
        patchedMethod.Add(new CodeInstruction(OpCodes.Brfalse_S, labelToOriginalMethod));
        // return;
        patchedMethod.Add(new CodeInstruction(OpCodes.Ret));

        instructions.First().labels.Add(labelToOriginalMethod);

        patchedMethod.AddRange(instructions);
        return patchedMethod;
    }
}
