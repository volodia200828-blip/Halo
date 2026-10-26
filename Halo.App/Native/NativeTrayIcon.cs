using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Halo.App.Views;
using Halo.Core.Services;
using SkiaSharp;

namespace Halo.App.Native;

public class NativeTrayIcon : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAY_CALLBACK = WM_USER + 101;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x00000010;

    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;

    private IntPtr _hWnd;
    private IntPtr _hIconActive = IntPtr.Zero;
    private IntPtr _hIconGameMode = IntPtr.Zero;
    private bool _isAdded = false;
    private bool _isDisposed = false;

    public event Action? OpenMainWindowRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ToggleGameModeRequested;
    public event Action? OpenShelfRequested;
    public event Action? ExitAppRequested;

    private HaloTrayWindow? _trayWindow;

    public void Initialize(IntPtr hWnd)
    {
        _hWnd = hWnd;
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Halo");
        Directory.CreateDirectory(appData);

        string activeIcoPath = Path.Combine(appData, "halo_active.ico");
        string gameIcoPath = Path.Combine(appData, "halo_game.ico");

        GenerateTrayIcons(activeIcoPath, gameIcoPath);

        int cx = GetSystemMetrics(SM_CXSMICON);
        if (cx <= 0) cx = 16;
        int cy = GetSystemMetrics(SM_CYSMICON);
        if (cy <= 0) cy = 16;

        if (File.Exists(activeIcoPath))
        {
            _hIconActive = LoadImage(IntPtr.Zero, activeIcoPath, IMAGE_ICON, cx, cy, LR_LOADFROMFILE);
        }
        if (File.Exists(gameIcoPath))
        {
            _hIconGameMode = LoadImage(IntPtr.Zero, gameIcoPath, IMAGE_ICON, cx, cy, LR_LOADFROMFILE);
        }

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY_CALLBACK,
            hIcon = _hIconActive,
            szTip = "Halo — Активно"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    public void UpdateStatus(bool isGameMode)
    {
        if (!_isAdded || _isDisposed) return;

        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        string tip = isGameMode
            ? (isEn ? "Halo — Game Mode (Triggers Paused)" : "Halo — Ігровий режим (Пауза)")
            : (isEn ? "Halo — Active" : "Halo — Активно");

        IntPtr currentIcon = isGameMode && _hIconGameMode != IntPtr.Zero
            ? _hIconGameMode
            : _hIconActive;

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1001,
            uFlags = NIF_ICON | NIF_TIP,
            hIcon = currentIcon,
            szTip = tip
        };

        Shell_NotifyIcon(NIM_MODIFY, ref data);
        _trayWindow?.RefreshStatusDisplay();
    }

    public void HandleTrayMessage(int lParam)
    {
        if (lParam == WM_LBUTTONUP || lParam == WM_RBUTTONUP)
        {
            ShowTrayFlyoutWindow();
        }
    }

    private void ShowTrayFlyoutWindow()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_trayWindow != null && _trayWindow.IsVisible)
            {
                _trayWindow.Activate();
                return;
            }

            _trayWindow = new HaloTrayWindow();
            _trayWindow.OpenMainWindowRequested += () => OpenMainWindowRequested?.Invoke();
            _trayWindow.OpenShelfRequested += () => OpenShelfRequested?.Invoke();
            _trayWindow.OpenSettingsRequested += () => OpenSettingsRequested?.Invoke();
            _trayWindow.ToggleGameModeRequested += () => ToggleGameModeRequested?.Invoke();
            _trayWindow.ExitAppRequested += () => ExitAppRequested?.Invoke();

            _trayWindow.Closed += (_, _) => _trayWindow = null;
            _trayWindow.Show();
            _trayWindow.Activate();
        });
    }

    private static void GenerateTrayIcons(string activePath, string gameModePath)
    {
        try
        {
            // Generate mathematically crisp vector icons at exact native tray dimensions
            CreateVectorIconFile(activePath, isGameMode: false);
            CreateVectorIconFile(gameModePath, isGameMode: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to generate tray icons: {ex.Message}");
        }
    }

    private static void CreateVectorIconFile(string outputPath, bool isGameMode)
    {
        int[] sizes = [16, 24, 32, 48];
        var pngDataList = new System.Collections.Generic.List<byte[]>();

        string avatarPath = Path.Combine(AppContext.BaseDirectory, "Resources", "avatar.png");
        SKBitmap? sourceAvatar = null;
        if (File.Exists(avatarPath))
        {
            try
            {
                sourceAvatar = SKBitmap.Decode(avatarPath);
            }
            catch { }
        }

        try
        {
            foreach (var s in sizes)
            {
                using var bmp = new SKBitmap(s, s, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bmp);
                canvas.Clear(SKColors.Transparent);

                if (sourceAvatar != null)
                {
                    using var paint = new SKPaint
                    {
                        IsAntialias = true
                    };

                    if (isGameMode)
                    {
                        // Warm flame / amber tint for game mode
                        paint.ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(255, 145, 0), SKBlendMode.Modulate);
                    }

                    var destRect = new SKRect(0, 0, s, s);
                    canvas.DrawBitmap(sourceAvatar, destRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
                }
                else
                {
                    // Fallback procedural rendering
                    float cx = s / 2f;
                    float cy = s / 2f;
                    float radius = s * 0.38f;
                    float stroke = Math.Max(1.35f, s * 0.095f);

                    SKColor colorStart = isGameMode ? new SKColor(255, 175, 0) : new SKColor(0, 240, 255);
                    SKColor colorEnd = isGameMode ? new SKColor(255, 69, 0) : new SKColor(139, 92, 246);

                    using var shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, 0),
                        new SKPoint(s, s),
                        [colorStart, colorEnd],
                        null,
                        SKShaderTileMode.Clamp);

                    using var ringPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = stroke,
                        IsAntialias = true,
                        Shader = shader
                    };
                    canvas.DrawCircle(cx, cy, radius, ringPaint);

                    float hHeight = radius * 1.05f;
                    float hWidth = radius * 0.78f;
                    float hStroke = Math.Max(1.1f, stroke * 0.8f);

                    using var hPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = hStroke,
                        StrokeCap = SKStrokeCap.Round,
                        IsAntialias = true,
                        Shader = shader
                    };

                    float leftX = cx - (hWidth / 2f);
                    float rightX = cx + (hWidth / 2f);
                    float topY = cy - (hHeight / 2f);
                    float botY = cy + (hHeight / 2f);

                    canvas.DrawLine(leftX, topY, leftX, botY, hPaint);
                    canvas.DrawLine(rightX, topY, rightX, botY, hPaint);
                    canvas.DrawLine(leftX, cy, rightX, cy, hPaint);
                }

                // Bottom-right status indicator badge
                float dotRadius = isGameMode ? (s >= 32 ? 4.5f : 2.5f) : (s >= 32 ? 3.5f : 2.0f);
                float dotX = s - dotRadius - 1;
                float dotY = s - dotRadius - 1;

                using var dotBg = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(16, 16, 22),
                    IsAntialias = true
                };
                canvas.DrawCircle(dotX, dotY, dotRadius + 0.8f, dotBg);

                using var dotFill = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = isGameMode ? new SKColor(255, 120, 0) : new SKColor(16, 185, 129),
                    IsAntialias = true
                };
                canvas.DrawCircle(dotX, dotY, dotRadius, dotFill);

                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                pngDataList.Add(data.ToArray());
            }
        }
        finally
        {
            sourceAvatar?.Dispose();
        }

        using var fs = File.Create(outputPath);
        using var bw = new BinaryWriter(fs);

        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)sizes.Length);

        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            byte[] bytes = pngDataList[i];
            bw.Write((byte)(s >= 256 ? 0 : s));
            bw.Write((byte)(s >= 256 ? 0 : s));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)bytes.Length);
            bw.Write((uint)offset);
            offset += bytes.Length;
        }

        foreach (var b in pngDataList)
        {
            bw.Write(b);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_isAdded)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _isAdded = false;
        }

        if (_hIconActive != IntPtr.Zero)
        {
            DestroyIcon(_hIconActive);
            _hIconActive = IntPtr.Zero;
        }
        if (_hIconGameMode != IntPtr.Zero)
        {
            DestroyIcon(_hIconGameMode);
            _hIconGameMode = IntPtr.Zero;
        }
    }
}
