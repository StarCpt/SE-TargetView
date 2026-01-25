using HarmonyLib;
using VRage.Render11.Resources;
using VRageRender;

namespace TargetView.Patches
{
    [HarmonyPatch]
    public static class Patch_MyRender11
    {
        public static IBorrowedRtvTexture? TargetViewTexture = null;

        [HarmonyPatch(typeof(MyRender11), nameof(MyRender11.DrawGameScene))]
        [HarmonyPrefix]
        public static void MyRender11_DrawGameScene_Prefix()
        {
            if (!Plugin.Settings.Enabled)
                return;

            // don't draw if a screenshot is being taken
            if (MyRender11.m_screenshot.HasValue)
                return;

            TargetViewManager.Draw();
        }

        [HarmonyPatch(typeof(MyRender11), nameof(MyRender11.DrawGameScene))]
        [HarmonyPostfix]
        public static void MyRender11_DrawGameScene_Postfix(IRtvBindable renderTarget)
        {
            if (TargetViewTexture is not null)
            {
                MyCopyToRT.Run(renderTarget, TargetViewTexture, customViewport: new MyViewport(0, 0, 500, 500), shouldStretch: false);
                MyRender11.RC.SetRtvNull();

                TargetViewTexture.Release();
                TargetViewTexture = null;
            }
        }
    }
}
