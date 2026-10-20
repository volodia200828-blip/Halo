using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Halo.App.Native;

public class HotkeyHelper : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;

    private const int MAIN_HOTKEY_ID = 9001;
    private const int GAMEMODE_HOTKEY_ID = 9002;

    private bool _isMainRegistered = false;
    private bool _isGameModeRegistered = false;

    public event Action? HotkeyPressed;
    public event Action? GameModeHotkeyPressed;

    public HotkeyHelper(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(HwndHook);
    }

    public bool Register(string hotkeyString)
    {
        UnregisterMain();

        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
            return false;

        _isMainRegistered = RegisterHotKey(_hwnd, MAIN_HOTKEY_ID, modifiers, vk);
        return _isMainRegistered;
    }

    public bool RegisterGameMode(string hotkeyString)
    {
        UnregisterGameMode();

        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
            return false;

        _isGameModeRegistered = RegisterHotKey(_hwnd, GAMEMODE_HOTKEY_ID, modifiers, vk);
        return _isGameModeRegistered;
    }

    private static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = MOD_NOREPEAT;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        Key key = Key.Space;
        var parts = hotkeyString.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var clean = p.Trim().ToLowerInvariant();
            if (clean == "alt") modifiers |= MOD_ALT;
            else if (clean == "ctrl" || clean == "control") modifiers |= MOD_CONTROL;
            else if (clean == "shift") modifiers |= MOD_SHIFT;
            else if (clean == "win") modifiers |= MOD_WIN;
            else if (Enum.TryParse<Key>(p, true, out var parsedKey))
            {
                key = parsedKey;
            }
        }

        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    public void UnregisterMain()
    {
        if (_isMainRegistered)
        {
            UnregisterHotKey(_hwnd, MAIN_HOTKEY_ID);
            _isMainRegistered = false;
        }
    }

    public void UnregisterGameMode()
    {
        if (_isGameModeRegistered)
        {
            UnregisterHotKey(_hwnd, GAMEMODE_HOTKEY_ID);
            _isGameModeRegistered = false;
        }
    }

    public void Unregister()
    {
        UnregisterMain();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == MAIN_HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            else if (id == GAMEMODE_HOTKEY_ID)
            {
                GameModeHotkeyPressed?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterMain();
        UnregisterGameMode();
        _source.RemoveHook(HwndHook);
    }
}
