using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Halo.App.Native;
using Halo.Core.Services;

namespace Halo.App;

public partial class App : Application
{
    public App()
    {
        // 1. WPF Dispatcher Unhandled Exceptions (UI thread safe recovery)
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                string logPath = CrashReporterService.Instance.LogCrash(e.Exception, "WPF Dispatcher UI Thread", isFatal: false);

                // Prevent fatal termination of Halo background service and tray
                e.Handled = true;

                try
                {
                    bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
                    string msg = isEn
                        ? $"Halo encountered an unexpected error, but your data and settings have been safely preserved.\n\nCrash dump:\n{logPath}\n\nError: {e.Exception.Message}"
                        : $"У роботі Halo виникла непередбачена помилка, але ваші налаштування та дані збережено.\n\nЗвіт про збій:\n{logPath}\n\nПомилка: {e.Exception.Message}";

                    MessageBox.Show(msg, isEn ? "Halo - Crash Protection & Safe Recovery" : "Halo - Захист від збоїв та безпечне відновлення", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { }
            }
            catch
            {
                CrashReporterService.Instance.SafeFlushUserData();
            }
        };

        // 2. AppDomain Unhandled Exceptions (Worker threads & fatal process crashes)
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                if (e.ExceptionObject is Exception ex)
                {
                    CrashReporterService.Instance.LogCrash(ex, "AppDomain Unhandled Exception", isFatal: e.IsTerminating);
                }
                else
                {
                    CrashReporterService.Instance.SafeFlushUserData();
                }
            }
            catch { }
        };

        // 3. TaskScheduler Unobserved Task Exceptions (Background Task / Async exceptions)
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                CrashReporterService.Instance.LogCrash(e.Exception, "TaskScheduler UnobservedTaskException", isFatal: false);
                e.SetObserved();
            }
            catch { }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        string? targetArg = e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0])
            ? e.Args[0]
            : null;

        string messageToSend = targetArg ?? "SHOW_MAIN_WINDOW";

        // Try connecting to existing running instance
        if (SingleInstanceIpc.TrySendToRunningInstance(messageToSend, 350))
        {
            // Another instance is running and was notified - terminate this process immediately
            Environment.Exit(0);
            return;
        }

        // We are the primary instance
        SingleInstanceIpc.StartServer();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceIpc.StopServer();
        base.OnExit(e);
    }
}
