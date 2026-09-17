using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using VRage.Generics;
using VRage.Render11.Resources;
using VRage.Render11.Resources.Internal;

namespace TargetView.Patches;

[HarmonyPatch]
public static class NullDepthStencilFix
{
    [HarmonyPatch(typeof(MyDepthStencilManager), nameof(MyDepthStencilManager.CreateDepthStencil))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // in MyDepthStencilManager.CreateDepthStencil the vanilla code allocates a new depth stencil obj using MyObjectPool.Allocate,
        // but this method doesn't create new objects (the bool nullAllowed parameter is ignored...) once the initial pool runs out (16 in this case)
        // so it just returns null causing an NRE in the next line when initializing the MyDepthStencil object

        MethodInfo targetMethod = AccessTools.DeclaredMethod(typeof(MyObjectsPool<MyDepthStencil>), nameof(MyObjectsPool<MyDepthStencil>.Allocate));

        foreach (var il in instructions)
        {
            if (il.opcode == OpCodes.Callvirt && (il.operand as MethodInfo) == targetMethod)
            {
                yield return new CodeInstruction(OpCodes.Call, ((Delegate)Allocate_Patch).Method);
            }
            else
            {
                yield return il;
            }
        }
    }

    private static MyDepthStencil Allocate_Patch(MyObjectsPool<MyDepthStencil> instance, bool nullAllowed)
    {
        if (nullAllowed)
        {
            return instance.Allocate(true);
        }
        else
        {
            instance.AllocateOrCreate(out var result);
            return result;
        }
    }
}
