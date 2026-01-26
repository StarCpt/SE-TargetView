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
    public class KeyBindingHandler
    {
        public MyKeys Key { get; private set; }
        public bool Recording
        {
            get => _listening;
            set
            {
                _listening = value;
                Control.Text = _listening ? $"{Key} (Recording)" : Key.ToString();
            }
        }
        public readonly MyGuiControlButton Control;
        private readonly Action<MyKeys> _onKeyChanged;

        private readonly List<MyKeys> _currentKeys = [];
        private bool _listening = false;

        public KeyBindingHandler(MyKeys initialKey, MyGuiControlButton control, Action<MyKeys> onKeyChanged)
        {
            Key = initialKey;
            Control = control;
            _onKeyChanged = onKeyChanged;

            SetKey(initialKey);
        }

        public void HandleInput()
        {
            if (!Recording)
                return;

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
            Control.Text = _listening ? $"{Key} (Recording)" : Key.ToString();
        }
    }

    public class MyGuiScreenPluginConfig : MyGuiScreenBase
    {
        private event Action OnHandleInput = delegate { };

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

        private KeyBindingHandler _hotKeyData;

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

            pos.Y += 0.02f;

            Vector2I resolution = screenRes;
            {
                const float TEXTBOX_WIDTH = 0.08f;

                MyGuiControlTextbox posXTextBox = AddIntTextBox(
                    "X Pos", pos with { X = -0.11f }, TEXTBOX_WIDTH,
                    settings.Position.X, 0, resolution.X,
                    val => settings.Position = settings.Position with { X = val });

                MyGuiControlTextbox sizeXTextBox = AddIntTextBox(
                    "Width", pos with { X = 0.06f }, TEXTBOX_WIDTH,
                    settings.Size.X, 0, resolution.X,
                    val => settings.Size = settings.Size with { X = val });
                pos.Y += sizeXTextBox.Size.Y + space;

                MyGuiControlTextbox posYTextBox = AddIntTextBox(
                    "Y Pos", pos with { X = -0.11f }, TEXTBOX_WIDTH,
                    settings.Position.Y, 0, resolution.Y,
                    val => settings.Position = settings.Position with { Y = val });

                MyGuiControlTextbox sizeYTextBox = AddIntTextBox(
                    "Height", pos with { X = 0.06f }, TEXTBOX_WIDTH,
                    settings.Size.Y, 0, resolution.Y,
                    val => settings.Size = settings.Size with { Y = val });
                pos.Y += sizeYTextBox.Size.Y + space;
            }

            pos.X = 0;
            pos.Y += 0.02f;

            var caption2 = AddCaption("Zoom Hotkey");
            caption2.PositionY = pos.Y;
            pos.Y += caption2.Size.Y;

            MyGuiControlButton hotKeyButton = AddKeyboardKeyBindingButton(pos, Plugin.Settings.ZoomKey, key => Plugin.Settings.ZoomKey = key, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
            pos.Y += hotKeyButton.Size.Y + space;

            // Bottom
            pos = new Vector2(0, (m_size!.Value.Y / 2) - space);
            MyGuiControlButton closeButton = new MyGuiControlButton(pos, text: MyTexts.Get(MyCommonTexts.Close), originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, onButtonClick: OnCloseClicked);
            Controls.Add(closeButton);
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            base.HandleInput(receivedFocusInThisUpdate);

            OnHandleInput?.Invoke();
        }

        private void HotKeyButton_ButtonClicked(MyGuiControlButton obj)
        {
            _hotKeyData.Recording = !_hotKeyData.Recording;
        }

        private void HotKeyButton_SecondaryButtonClicked(MyGuiControlButton obj)
        {
            _hotKeyData.SetKey(MyKeys.None);
        }
        
        private MyGuiControlTextbox AddIntTextBox(string caption, Vector2 position, float width, int initialValue, int min, int max, Action<int> setter, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
        {
            MyGuiControlTextbox control = new MyGuiControlTextbox(position, initialValue.ToString(), max.ToString().Length + 1, type: MyGuiControlTextboxType.DigitsOnly, minNumericValue: min, maxNumericValue: max)
            {
                Size = new Vector2(width, 0), // y doesn't do anything
                OriginAlign = originAlign,
            };
            control.TextChanged += OnTextChanged;
            AddControl(control);
            AddCaption(control, caption);
            return control;

            void OnTextChanged(MyGuiControlTextbox box)
            {
                if (int.TryParse(box.Text, out int val))
                {
                    val = MathHelper.Clamp(val, min, max);
                    setter.Invoke(val);
                }
            }
        }

        private MyGuiControlButton AddKeyboardKeyBindingButton(Vector2 position, MyKeys initialValue, Action<MyKeys> setter, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
        {
            MyGuiControlButton button = new MyGuiControlButton(position, visualStyle: MyGuiControlButtonStyleEnum.ControlSetting, cueEnum: GuiSounds.MouseClick)
            {
                OriginAlign = originAlign,
            };

            KeyBindingHandler handler = new KeyBindingHandler(initialValue, button, setter);
            button.UserData = handler;

            OnHandleInput += handler.HandleInput;
            button.ButtonClicked += _ => handler.Recording = !handler.Recording;
            button.SecondaryButtonClicked += _ => handler.SetKey(MyKeys.None);

            AddControl(button);
            
            return button;
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
