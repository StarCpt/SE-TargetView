using TargetView.Gui;
using HarmonyLib;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using System;
using System.Reflection;
using VRage;
using VRage.Plugins;

namespace TargetView
{
    public class Plugin : IPlugin
    {
        public static TargetViewSettings Settings { get; private set; }
        public static Boxed<(uint CharacterActorId, string[] MaterialsDisabledInFirst)>? FirstPersonCharacter = null;

        public Plugin()
        {
            Settings = TargetViewSettings.Load();
        }

        public void Init(object gameInstance)
        {
            new Harmony(nameof(TargetView)).PatchAll(Assembly.GetExecutingAssembly());
        }

        private uint _counter = 0;
        public void Update()
        {
            if (!Settings.Enabled)
                return;

            if (MySession.Static != null && MySession.Static.Ready)
            {
                TargetViewManager.Update();
            }

            if (++_counter % 10 != 0)
                return;

            if (MySession.Static?.CameraController?.Entity is MyCharacter character && (character.IsInFirstPersonView || character.ForceFirstPersonCamera))
            {
                FirstPersonCharacter = new((character.Render.GetRenderObjectID(), character.Definition.MaterialsDisabledIn1st));
            }
            else
            {
                FirstPersonCharacter = null;
            }
        }

        public void OpenConfigDialog()
        {
            MyGuiSandbox.AddScreen(new MyGuiScreenPluginConfig());
        }

        public void Dispose()
        {
        }
    }
}
