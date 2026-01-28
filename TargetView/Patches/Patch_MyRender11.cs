using HarmonyLib;
using SharpDX.Direct3D11;
using VRage.Render11.Resources;
using VRageRender;

namespace TargetView.Patches;

[HarmonyPatch]
public static class Patch_MyRender11
{
    [HarmonyPatch(typeof(MyRender11), nameof(MyRender11.DrawGameScene))]
    [HarmonyPostfix]
    public static void MyRender11_DrawGameScene_Postfix(IRtvBindable renderTarget)
    {
        if (renderTarget is null || !Plugin.Settings.Enabled)
            return;

        bool success = TargetViewManager.Draw(renderTarget.Rtv.Description.Format, out var targetViewTexture, out var targetViewViewport);
        if (success)
        {
            var srcRegion = new ResourceRegion
            {
                Left = 0,
                Top = 0,
                Front = 0,
                Back = 1,
                Right = (int)targetViewViewport.Width,
                Bottom = (int)targetViewViewport.Height,
            };
            int destX = (int)targetViewViewport.OffsetX;
            int destY = (int)targetViewViewport.OffsetY;
            MyRender11.RC.CopySubresourceRegion(targetViewTexture, 0, srcRegion, renderTarget, 0, destX, destY, 0);

            targetViewTexture.Release();
        }
    }
}
