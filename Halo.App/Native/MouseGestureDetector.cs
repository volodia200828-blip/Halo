using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Halo.Core.Services;

namespace Halo.App.Native;

public class MouseGestureDetector : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_MButton = 0x04;

    private readonly DispatcherTimer _timer;
    private readonly List<(DateTime Time, int X, int Y)> _samples = new();
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private bool _wasMiddleDown = false;

    public bool IsPaused { get; set; } = false;

    public event Action<Point>? ShakeDetected;
    public event Action<Point>? MiddleClickDetected;

    public MouseGestureDetector()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(22)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (IsPaused) return;

        var settings = SettingsService.Instance.Current;
        if (settings.IsGameModeEnabled) return;

        if (!GetCursorPos(out var pt)) return;
        var now = DateTime.UtcNow;

        // 1. Check Middle Mouse Button click
        bool isMiddleDown = (GetAsyncKeyState(VK_MButton) & 0x8000) != 0;
        if (settings.EnableMiddleMouseTrigger && isMiddleDown && !_wasMiddleDown)
        {
            if ((now - _lastTriggerTime).TotalMilliseconds > 800)
            {
                _lastTriggerTime = now;
                MiddleClickDetected?.Invoke(new Point(pt.X, pt.Y));
            }
        }
        _wasMiddleDown = isMiddleDown;

        // 2. Check Shake gesture (rapid horizontal or vertical reversals)
        if (!settings.EnableDragShakeTrigger)
        {
            _samples.Clear();
            return;
        }

        _samples.Add((now, pt.X, pt.Y));
        _samples.RemoveAll(s => (now - s.Time).TotalMilliseconds > 520);

        if (_samples.Count < 8 || (now - _lastTriggerTime).TotalMilliseconds < 1400)
            return;

        // Count significant direction reversals along X axis
        int reversals = 0;
        int lastDirection = 0;
        int anchorX = _samples[0].X;

        for (int i = 1; i < _samples.Count; i++)
        {
            int dx = _samples[i].X - anchorX;
            if (Math.Abs(dx) >= 38)
            {
                int dir = dx > 0 ? 1 : -1;
                if (lastDirection != 0 && dir != lastDirection)
                {
                    reversals++;
                }
                lastDirection = dir;
                anchorX = _samples[i].X;
            }
        }

        if (reversals >= 3)
        {
            _samples.Clear();
            _lastTriggerTime = now;
            ShakeDetected?.Invoke(new Point(pt.X, pt.Y));
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
