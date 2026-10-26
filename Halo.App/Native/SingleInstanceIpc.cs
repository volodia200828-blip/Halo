using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Halo.App.Native;

public static class SingleInstanceIpc
{
    private const string PipeName = "Halo_App_SingleInstance_Pipe";
    public static event Action<string>? FilePathReceived;
    public static event Action? ShowMainWindowRequested;
    private static CancellationTokenSource? _cts;

    public static bool TrySendToRunningInstance(string message, int timeoutMs = 350)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void StartServer()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    string? line = await reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (line == "SHOW_MAIN_WINDOW")
                        {
                            ShowMainWindowRequested?.Invoke();
                        }
                        else
                        {
                            FilePathReceived?.Invoke(line);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(100, token);
                }
            }
        }, token);
    }

    public static void StopServer()
    {
        _cts?.Cancel();
    }
}
