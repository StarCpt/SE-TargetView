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

namespace TargetView.Gui;

public class MyGuiScreenPluginConfig : MyGuiScreenBase
{
    private event Action OnHandleInput = delegate { };

    private const float space = 0.01f;
    private Vector2I screenRes;

    private static readonly Vector2I _minSize = new Vector2I(50, 50);

    public MyGuiScreenPluginConfig() : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.47f, 0.85f), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
    {
        EnabledBackgroundFade = true;
        CloseButtonEnabled = true;
        screenRes = MyRender11.BackBufferResolution;
    }

    public override string GetFriendlyName() => GetType().FullName!;

    public override void LoadContent()
    {
        base.LoadContent();
        RecreateControls(false);
    }

    public override void RecreateControls(bool constructor)
    {
        TargetViewSettings settings = Plugin.Settings;

        MyGuiControlLabel caption = AddCaption("Target View Settings");
        Vector2 pos = caption.Position;
        pos.Y += (caption.Size.Y / 2) + space;

        MyGuiControlSeparatorList seperators = new MyGuiControlSeparatorList();
        float sepWidth = Size!.Value.X * 0.8f;
        seperators.AddHorizontal(pos - new Vector2(sepWidth / 2, 0), sepWidth);
        Controls.Add(seperators);
        pos.Y += space;

        MyGuiControlCheckbox enabledCheckbox = new MyGuiControlCheckbox(pos, isChecked: settings.Enabled, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
        enabledCheckbox.IsCheckedChanged += cb => Plugin.Settings.Enabled = cb.IsChecked;
        Controls.Add(enabledCheckbox);
        AddCaption(enabledCheckbox, "Enabled");
        pos.Y += enabledCheckbox.Size.Y + space;

        pos.X -= 0.06f;
        pos.Y += space;

        {
            const float TEXTBOX_WIDTH = 0.08f;

            Vector2I resolution = screenRes;

            MyGuiControlTextbox posXTextBox = AddIntTextBox(
                "X Pos", pos with { X = -0.11f }, TEXTBOX_WIDTH,
                settings.Position.X, 0, resolution.X,
                val => settings.Position = settings.Position with { X = val });

            MyGuiControlTextbox sizeXTextBox = AddIntTextBox(
                "Width", pos with { X = 0.06f }, TEXTBOX_WIDTH,
                settings.Size.X, _minSize.X, resolution.X,
                val => settings.Size = settings.Size with { X = val });
            pos.Y += sizeXTextBox.Size.Y + space;

            MyGuiControlTextbox posYTextBox = AddIntTextBox(
                "Y Pos", pos with { X = -0.11f }, TEXTBOX_WIDTH,
                settings.Position.Y, 0, resolution.Y,
                val => settings.Position = settings.Position with { Y = val });

            MyGuiControlTextbox sizeYTextBox = AddIntTextBox(
                "Height", pos with { X = 0.06f }, TEXTBOX_WIDTH,
                settings.Size.Y, _minSize.Y, resolution.Y,
                val => settings.Size = settings.Size with { Y = val });
            pos.Y += sizeYTextBox.Size.Y + space;
        }

        pos.X = 0;
        pos.Y += 0.02f;

        // border
        {
            MyGuiControlColor borderColorPicker = AddColorPicker(
                "Border Color", pos, settings.BorderColor, Color.White,
                color => settings.BorderColor = color,
                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
            pos.Y += borderColorPicker.Size.Y - 0.01f;

            MyGuiControlSlider borderThicknessSlider = AddIntSlider(
                "Border Thickness", pos with { X = -0.06f }, 0.2f,
                Plugin.Settings.BorderThickness, 0, 20, 1,
                val => Plugin.Settings.BorderThickness = val,
                val => val.ToString("0 px"));
            pos.Y += borderThicknessSlider.Size.Y + space;
        }

        pos.Y += 0.005f;

        MyGuiControlTextbox minDistTextBox = AddIntTextBox("Min Distance", pos with { X = -0.0025f }, 0.1315f, settings.MinDistance, 0, 100000, val => settings.MinDistance = val);
        pos.Y += minDistTextBox.Size.Y + space;
        pos.Y += 0.005f;

        MyGuiControlSlider zoomSpeedSlider = AddFloatSlider(
            "Zoom Speed", pos with { X = -0.003f }, 0.133f,
            settings.ZoomSpeed, 1, 5, 3,
            val => settings.ZoomSpeed = val,
            val => val.ToString("0.00"));
        pos.Y += zoomSpeedSlider.Size.Y + space;

        MyGuiControlButton zoomBindingButton = AddKeyboardKeyBindingButton(pos, Plugin.Settings.ZoomKey, key => Plugin.Settings.ZoomKey = key, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
        AddCaption(zoomBindingButton, "Zoom Key", -0.005f);

        MyGuiControlCheckbox zoomToggleCheckbox = new(new Vector2(pos.X + 0.135f, pos.Y - 0.0045f), toolTip: "Off: Hold to zoom\nOn: Toggles zoom", isChecked: settings.ToggleZoom, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
        zoomToggleCheckbox.IsCheckedChanged += _ => settings.ToggleZoom = zoomToggleCheckbox.IsChecked;
        AddControl(zoomToggleCheckbox);
        AddCaption(zoomToggleCheckbox, "Toggle", xOffset: 0.085f);

        pos.Y += zoomBindingButton.Size.Y + space;

        {
            MyGuiControlButton painterBindingButton = AddKeyboardKeyBindingButton(pos, Plugin.Settings.PainterKey, key => Plugin.Settings.PainterKey = key, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            painterBindingButton.SetToolTip("Only available when using WeaponCore.\nShows paint cursor in target view window\nand disables gyro control when this key is pressed.");
            AddCaption(painterBindingButton, "Painter Key", -0.005f);
            pos.Y += painterBindingButton.Size.Y + space;

            MyGuiControlColor painterCursorColorPicker = AddColorPicker(
                "Painter Color", pos, settings.PainterCursorColor, Color.White,
                color => settings.PainterCursorColor = color,
                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
            pos.Y += painterCursorColorPicker.Size.Y - 0.01f;

            MyGuiControlSlider painterCursorSizeSlider = AddIntSlider(
                "Painter Size", pos with { X = -0.06f }, 0.2f,
                settings.PainterCursorSize, 16, 64, 32,
                val => settings.PainterCursorSize = val,
                val => val.ToString("0 px"));
            pos.Y += painterCursorSizeSlider.Size.Y + space;
        }

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

    private MyGuiControlColor AddColorPicker(string caption, Vector2 position, Color initialColor, Color defaultColor, Action<Color> setter, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
    {
        MyGuiControlColor control = new MyGuiControlColor("", 1, position, initialColor, defaultColor, MyStringId.NullOrEmpty)
        {
            OriginAlign = originAlign,
        };
        control.OnChange += _ => setter.Invoke(control.Color);
        AddControl(control);
        AddControl(new MyGuiControlLabel(new Vector2(position.X - control.Size.X * 0.5f, position.Y - 0.005f), text: caption, textScale: 0.8f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP));
        return control;
    }

    private MyGuiControlSlider AddIntSlider(string caption, Vector2 position, float width, int initialValue, int minValue, int maxValue, int defaultValue, Action<int> setter, Func<int, string> valueToTextFunc, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
    {
        MyGuiControlSlider control = new MyGuiControlSlider(position, minValue, maxValue, width, defaultValue, intValue: true, originAlign: originAlign);
        control.Value = initialValue;
        control.ValueChanged += _ => setter.Invoke((int)control.Value);
        AddControl(control);
        AddCaption(control, caption);
        AddCustomSliderLabel(control, val => valueToTextFunc.Invoke((int)val));
        return control;
    }

    private MyGuiControlSlider AddFloatSlider(string caption, Vector2 position, float width, float initialValue, float minValue, float maxValue, float defaultValue, Action<float> setter, Func<float, string> valueToTextFunc, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
    {
        MyGuiControlSlider control = new MyGuiControlSlider(position, minValue, maxValue, width, defaultValue, originAlign: originAlign);
        control.Value = initialValue;
        control.ValueChanged += _ => setter.Invoke(control.Value);
        AddControl(control);
        AddCaption(control, caption);
        AddCustomSliderLabel(control, val => valueToTextFunc.Invoke(val));
        return control;
    }

    private MyGuiControlTextbox AddIntTextBox(string caption, Vector2 position, float width, int initialValue, int min, int max, Action<int> setter, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
    {
        MyGuiControlTextbox control = new MyGuiControlTextbox(position, initialValue.ToString(), max.ToString().Length + 1, type: MyGuiControlTextboxType.DigitsOnly, minNumericValue: 0, maxNumericValue: max)
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

    private void AddCaption(MyGuiControlBase control, string caption, float yOffset = 0, float xOffset = 0)
    {
        Controls.Add(new MyGuiControlLabel(control.Position + new Vector2(-space + xOffset, control.Size.Y / 2 + yOffset), text: caption, originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER));
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
}
