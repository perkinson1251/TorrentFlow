using Avalonia;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace TorrentFlow;

internal class Program
{
    private static readonly string PipeName = "TorrentFlow_SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        bool isFirstInstance;
        using (var mutex = new Mutex(true, "TorrentFlow_Mutex", out isFirstInstance))
        {
            if (!isFirstInstance)
            {
                if (args.Length > 0)
                    using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        client.Connect(500);
                        using (var writer = new StreamWriter(client))
                        {
                            writer.WriteLine(args[0]);
                        }
                    }

                return;
            }

            StartPipeServer();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static void StartPipeServer()
    {
        new Thread(() =>
            {
                while (true)
                    using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In))
                    {
                        server.WaitForConnection();
                        using (var reader = new StreamReader(server))
                        {
                            var filePath = reader.ReadLine();
                            if (!string.IsNullOrEmpty(filePath)) App.OnFileOpened(filePath);
                        }
                    }
            })
            { IsBackground = true }.Start();
    }
}