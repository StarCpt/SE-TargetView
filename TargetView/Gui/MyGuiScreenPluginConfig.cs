using Sandbox;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Audio;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace TargetView.Gui
{
    public class MyGuiScreenPluginConfig : MyGuiScreenBase
    {
        private const float space = 0.01f;
        private Vector2I screenRes;

        private static readonly Vector2I _minSize = new Vector2I(50, 50);

        public MyGuiScreenPluginConfig() : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.47f, 0.6f), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
        {
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;
            screenRes = MyRender11.BackBufferResolution;
        }

        public override string GetFriendlyName() => GetType().Name;

        public override void LoadContent()
        {
            base.LoadContent();
            RecreateControls(false);
        }

        private ControlButtonData _hotKeyData;

        public override void RecreateControls(bool constructor)
        {
            TargetViewSettings settings = Plugin.Settings;

            MyGuiControlLabel caption = AddCaption("Target View Settings");
            Vector2 pos = caption.Position;
            pos.Y += (caption.Size.Y / 2) + space;

            MyGuiControlSeparatorList seperators = new MyGuiControlSeparatorList();
            float sepWidth = Size.Value.X * 0.8f;
            seperators.AddHorizontal(pos - new Vector2(sepWidth / 2, 0), sepWidth);
            Controls.Add(seperators);
            pos.Y += space;

            MyGuiControlCheckbox enabledCheckbox = new MyGuiControlCheckbox(pos, isChecked: settings.Enabled, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            enabledCheckbox.IsCheckedChanged += IsEnabledCheckedChanged;
            Controls.Add(enabledCheckbox);
            AddCaption(enabledCheckbox, "Enabled");
            pos.Y += enabledCheckbox.Size.Y + space;

            pos.X -= 0.06f;

            MyGuiControlSlider ratioSlider = new MyGuiControlSlider(pos, 1, 30, 0.2f, settings.Ratio, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, intValue: true);
            ratioSlider.SetToolTip("Render camera view every nth frame.");
            ratioSlider.ValueChanged += RenderRatioChanged;
            Controls.Add(ratioSlider);
            AddCaption(ratioSlider, "Render ratio");
            AddCustomSliderLabel(ratioSlider, val => $"{val}x");
            pos.Y += ratioSlider.Size.Y + space;

            pos.Y += 0.02f;

            //MyGuiControlCheckbox headFixCheckbox = new MyGuiControlCheckbox(pos, isChecked: settings.HeadFix, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            //headFixCheckbox.SetToolTip("Fix invisible character head on camera lcd in 1st person view.\nMay cause issues with modded characters.");
            //headFixCheckbox.IsCheckedChanged += IsHeadfixCheckedChanged;
            //Controls.Add(headFixCheckbox);
            //AddCaption(headFixCheckbox, "Head fix");
            //pos.Y += headFixCheckbox.Size.Y + space;
            //
            //MyGuiControlCheckbox occlusionFixCheckbox = new MyGuiControlCheckbox(pos, isChecked: settings.OcclusionFix, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            //occlusionFixCheckbox.SetToolTip("Disable occlusion culling while drawing the camera view to\nfix invisible entities caused by incorrect culling.\nMay lower FPS depending on the scene.");
            //occlusionFixCheckbox.IsCheckedChanged += IsOcclusionfixCheckedChanged;
            //Controls.Add(occlusionFixCheckbox);
            //AddCaption(occlusionFixCheckbox, "Occlusion fix");
            //pos.Y += occlusionFixCheckbox.Size.Y + space;

            Vector2I resolution = screenRes;
            {
                MyGuiControlTextbox posXTextBox = new MyGuiControlTextbox(pos with { X = -0.11f }, settings.Position.X.ToString(), 6, type: MyGuiControlTextboxType.DigitsOnly, minNumericValue: 0, maxNumericValue: resolution.X)
                {
                    Size = new Vector2(0.08f, 0),
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                };
                posXTextBox.TextChanged += PosXTextBox_TextChanged;
                AddControl(posXTextBox);
                AddCaption(posXTextBox, "X Pos");

                MyGuiControlTextbox sizeXTextBox = new MyGuiControlTextbox(pos with { X = 0.06f }, settings.Size.X.ToString(), 6, type: MyGuiControlTextboxType.DigitsOnly, minNumericValue: 0, maxNumericValue: resolution.X)
                {
                    Size = new Vector2(0.08f, 0),
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                };
                sizeXTextBox.TextChanged += SizeXTextBox_TextChanged;
                AddControl(sizeXTextBox);
                AddCaption(sizeXTextBox, "X Size");
                pos.Y += sizeXTextBox.Size.Y + space;

                MyGuiControlTextbox posYTextBox = new MyGuiControlTextbox(pos with { X = -0.11f }, settings.Position.Y.ToString(), 6, type: MyGuiControlTextboxType.DigitsOnly, minNumericValue: 0, maxNumericValue: resolution.Y)
                {
                    Size = new Vector2(0.08f, 0),
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                };
                posYTextBox.TextChanged += PosYTextBox_TextChanged;
                AddControl(posYTextBox);
                AddCaption(posYTextBox, "Y Pos");

                MyGuiControlTextbox sizeYTextBox = new MyGuiControlTextbox(pos with { X = 0.06f }, settings.Size.Y.ToString(), 6, type: MyGuiControlTextboxType.DigitsOnly, minNumericValue: 0, maxNumericValue: resolution.Y)
                {
                    Size = new Vector2(0.08f, 0),
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                };
                sizeYTextBox.TextChanged += SizeYTextBox_TextChanged;
                AddControl(sizeYTextBox);
                AddCaption(sizeYTextBox, "Y Size");
                pos.Y += sizeYTextBox.Size.Y + space;
            }

            pos.Y += 0.02f;
            var caption2 = AddCaption("Zoom Hotkey");
            caption2.PositionY = pos.Y;
            pos.Y += caption2.Size.Y;

            MyGuiControlButton hotKeyButton = new MyGuiControlButton(pos, visualStyle: MyGuiControlButtonStyleEnum.ControlSetting, cueEnum: GuiSounds.MouseClick)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
            };
            hotKeyButton.UserData = _hotKeyData = new ControlButtonData(Plugin.Settings.ZoomKey, hotKeyButton, key => Plugin.Settings.ZoomKey = key);
            hotKeyButton.ButtonClicked += HotKeyButton_ButtonClicked;
            hotKeyButton.SecondaryButtonClicked += HotKeyButton_SecondaryButtonClicked;
            AddControl(hotKeyButton);
            pos.Y += hotKeyButton.Size.Y + space;

            // Bottom
            pos = new Vector2(0, (m_size!.Value.Y / 2) - space);
            MyGuiControlButton closeButton = new MyGuiControlButton(pos, text: MyTexts.Get(MyCommonTexts.Close), originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, onButtonClick: OnCloseClicked);
            Controls.Add(closeButton);
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            base.HandleInput(receivedFocusInThisUpdate);

            if (_hotKeyData.Control.HasFocus)
            {
                _hotKeyData.HandleInput();
            }
        }

        class ControlButtonData
        {
            public MyKeys Key { get; private set; }
            public readonly MyGuiControlButton Control;
            private readonly Action<MyKeys> _onKeyChanged;

            private readonly List<MyKeys> _currentKeys = [];

            public ControlButtonData(MyKeys initialKey, MyGuiControlButton control, Action<MyKeys> onKeyChanged)
            {
                Key = initialKey;
                Control = control;
                _onKeyChanged = onKeyChanged;

                SetKey(initialKey);
            }

            public void HandleInput()
            {
                _currentKeys.Clear();
                MyInput.Static.GetPressedKeys(_currentKeys);
                foreach (MyKeys key in _currentKeys)
                {
                    if (MyInput.Static.IsNewKeyPressed(key) && MyInput.Static.IsKeyValid(key) && Key != key)
                    {
                        SetKey(key);
                        return;
                    }
                }
            }

            public void SetKey(MyKeys key)
            {
                Key = key;
                _onKeyChanged?.Invoke(key);
                Control.Text = Key.ToString();
            }
        }

        private void HotKeyButton_ButtonClicked(MyGuiControlButton obj)
        {
        }

        private void HotKeyButton_SecondaryButtonClicked(MyGuiControlButton obj)
        {
            _hotKeyData.SetKey(MyKeys.None);
        }

        private void PosXTextBox_TextChanged(MyGuiControlTextbox box)
        {
            if (int.TryParse(box.Text, out var result))
            {
                Plugin.Settings.Position = Plugin.Settings.Position with { X = MathHelper.Clamp(result, 0, screenRes.X) };
            }
        }

        private void PosYTextBox_TextChanged(MyGuiControlTextbox box)
        {
            if (int.TryParse(box.Text, out var result))
            {
                Plugin.Settings.Position = Plugin.Settings.Position with { Y = MathHelper.Clamp(result, 0, screenRes.Y) };
            }
        }

        private void SizeXTextBox_TextChanged(MyGuiControlTextbox box)
        {
            if (int.TryParse(box.Text, out var result))
            {
                Plugin.Settings.Size = Plugin.Settings.Size with { X = MathHelper.Clamp(result, _minSize.X, screenRes.X) };
            }
        }

        private void SizeYTextBox_TextChanged(MyGuiControlTextbox box)
        {
            if (int.TryParse(box.Text, out var result))
            {
                Plugin.Settings.Size = Plugin.Settings.Size with { Y = MathHelper.Clamp(result, _minSize.Y, screenRes.Y) };
            }
        }

        private void AddCaption(MyGuiControlBase control, string caption)
        {
            Controls.Add(new MyGuiControlLabel(control.Position + new Vector2(-space, control.Size.Y / 2), text: caption, originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER));
        }

        private void AddCustomSliderLabel(MyGuiControlSlider slider, Func<float, string> valueToTextFunc)
        {
            MyGuiControlLabel label = new()
            {
                Position = slider.Position + new Vector2(slider.Size.X + space, slider.Size.Y / 2),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                Text = valueToTextFunc(slider.Value)
            };
            slider.ValueChanged += s => label.Text = valueToTextFunc(s.Value);
            Controls.Add(label);
        }

        private void OnCloseClicked(MyGuiControlButton btn)
        {
            CloseScreen();
        }

        protected override void OnClosed()
        {
            Plugin.Settings.Save();
        }

        void IsEnabledCheckedChanged(MyGuiControlCheckbox cb) => Plugin.Settings.Enabled = cb.IsChecked;
        void RenderRatioChanged(MyGuiControlSlider slider) => Plugin.Settings.Ratio = (int)slider.Value;
        void IsHeadfixCheckedChanged(MyGuiControlCheckbox cb) => Plugin.Settings.HeadFix = cb.IsChecked;
        void IsOcclusionfixCheckedChanged(MyGuiControlCheckbox cb) => Plugin.Settings.OcclusionFix = cb.IsChecked;
    }
}
