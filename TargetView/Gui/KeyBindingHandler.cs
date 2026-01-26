using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using VRage.Input;

namespace TargetView.Gui;

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
            if (MyInput.Static.IsNewKeyPressed(key) && MyInput.Static.IsKeyValid(key))
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
        Recording = false;
    }
}
