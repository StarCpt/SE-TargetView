using HarmonyLib;
using SharpDX.Direct3D11;
using VRage.Render11.Resources;
using VRageRender;

namespace TargetView.Patches
{
    [HarmonyPatch]
    public static class Patch_MyRender11
    {
        public static IBorrowedRtvTexture? TargetViewTexture = null;
        public static MyViewport TargetViewViewport;

        [HarmonyPatch(typeof(MyRender11), nameof(MyRender11.DrawGameScene))]
        [HarmonyPrefix]
        public static void MyRender11_DrawGameScene_Prefix(IRtvBindable renderTarget)
        {
            if (!Plugin.Settings.Enabled)
                return;

            TargetViewManager.Draw(renderTarget.Rtv.Description.Format);
        }

        [HarmonyPatch(typeof(MyRender11), nameof(MyRender11.DrawGameScene))]
        [HarmonyPostfix]
        public static void MyRender11_DrawGameScene_Postfix(IRtvBindable renderTarget)
        {
            if (TargetViewTexture is not null)
            {
                //MyCopyToRT.Run(renderTarget, TargetViewTexture, customViewport: TargetViewViewport, shouldStretch: true);
                //MyRender11.RC.SetRtvNull();

                var srcRegion = new ResourceRegion
                {
                    Left = 0,
                    Top = 0,
                    Front = 0,
                    Back = 1,
                    Right = (int)TargetViewViewport.Width,
                    Bottom = (int)TargetViewViewport.Height,
                };
                int destX = (int)TargetViewViewport.OffsetX;
                int destY = (int)TargetViewViewport.OffsetY;
                MyRender11.RC.CopySubresourceRegion(TargetViewTexture, 0, srcRegion, renderTarget, 0, destX, destY, 0);

                TargetViewTexture.Release();
                TargetViewTexture = null;
            }
        }
    }
}
