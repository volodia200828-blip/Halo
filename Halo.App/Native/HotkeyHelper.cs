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
    private int _currentId = 9000;
    private bool _isRegistered = false;

    public event Action? HotkeyPressed;

    public HotkeyHelper(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(HwndHook);
    }

    public bool Register(string hotkeyString)
    {
        Unregister();

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        uint modifiers = MOD_NOREPEAT;
        Key key = Key.Space;

        var parts = hotkeyString.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var clean = p.Trim().ToLowerInvariant();
            if (clean == "alt") modifiers |= MOD_ALT;
            else if (clean == "ctrl" || clean == "control") modifiers |= MOD_CONTROL;
            else if (clean == "shift") modifiers |= MOD_SHIFT;
            else if (clean == "win") modifiers |= MOD_WIN;
            else
            {
                if (Enum.TryParse<Key>(p, true, out var parsedKey))
                {
                    key = parsedKey;
                }
            }
        }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _currentId++;
        _isRegistered = RegisterHotKey(_hwnd, _currentId, modifiers, vk);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            UnregisterHotKey(_hwnd, _currentId);
            _isRegistered = false;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _currentId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(HwndHook);
    }
}
