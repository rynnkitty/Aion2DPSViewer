using Aion2DPSViewer.Forms;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aion2DPSViewer.Core;

public class HotkeyManager
{
    private const uint MOD_ALT = 1;
    private const uint MOD_CTRL = 2;
    private const uint MOD_SHIFT = 4;
    private const uint MOD_NOREPEAT = 0x4000;
    private readonly OverlayForm _form;
    private readonly List<int> _registeredIds = new List<int>();
    private int _nextId = 1;
    private bool _suspended;
    private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotkeyManager(OverlayForm form) => _form = form;

    public void RegisterFromSettings()
    {
        UnregisterAll();
        ShortcutSettings shortcuts = AppSettings.Instance.Shortcuts;
        TryRegister(shortcuts.Toggle, () => _form.ToggleVisibility());
        TryRegister(shortcuts.Refresh, () => _form.TriggerClearShortcut());
        TryRegister(shortcuts.Compact, () => _form.ToggleCompact());
        TryRegister(shortcuts.SwitchTab, () => _form.TriggerSwitchTab());
    }

    public void Suspend()
    {
        _suspended = true;
        foreach (int registeredId in _registeredIds)
            UnregisterHotKey(_form.Handle, registeredId);
    }

    public void Resume()
    {
        _suspended = false;
        RegisterFromSettings();
    }

    public void UnregisterAll()
    {
        foreach (int registeredId in _registeredIds)
            UnregisterHotKey(_form.Handle, registeredId);
        _registeredIds.Clear();
        _actions.Clear();
        _nextId = 1;
    }

    private void TryRegister(string? accelerator, Action action)
    {
        if (string.IsNullOrEmpty(accelerator) || !ParseAccelerator(accelerator, out uint modifiers, out uint vk))
            return;
        int id = _nextId++;
        if (!RegisterHotKey(_form.Handle, id, modifiers | 0x4000U, vk))
            return;
        _registeredIds.Add(id);
        _actions[id] = action;
    }

    public void ProcessHotkey(int id)
    {
        if (_suspended || !_actions.TryGetValue(id, out Action action))
            return;
        action();
    }

    private static bool ParseAccelerator(string accel, out uint modifiers, out uint vk)
    {
        modifiers = 0U;
        vk = 0U;
        foreach (string str1 in accel.Split(new[] { '+' }, StringSplitOptions.None))
        {
            string str2 = str1.Trim();
            string lower = str2.ToLower();
            bool handled = true;
            switch (lower)
            {
                case "alt":
                    modifiers |= 1U;
                    break;
                case "ctrl":
                case "control":
                    modifiers |= 2U;
                    break;
                case "shift":
                    modifiers |= 4U;
                    break;
                case "`":
                case "~":
                case "backquote":
                    vk = 0xC0U;
                    break;
                default:
                    handled = false;
                    break;
            }
            if (!handled)
            {
                if (str2.Length == 1 && char.IsDigit(str2[0]))
                    vk = (uint)str2[0];
                else if (str2.Length == 1 && char.IsLetter(str2[0]))
                    vk = (uint)char.ToUpper(str2[0]);
                else if (Enum.TryParse<Keys>(str2, true, out Keys keys))
                    vk = (uint)keys;
            }
        }
        return vk > 0U;
    }
}
