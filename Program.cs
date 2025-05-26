using Avalonia;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace TorrentFlow;

class Program
{
    private static readonly string PipeName = "TorrentFlow_SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        bool isFirstInstance;
        using (Mutex mutex = new Mutex(true, "TorrentFlow_Mutex", out isFirstInstance))
        {
            if (!isFirstInstance)
            {
                if (args.Length > 0)
                {
                    using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        client.Connect(500);
                        using (StreamWriter writer = new StreamWriter(client))
                        {
                            writer.WriteLine(args[0]);
                        }
                    }
                }
                return;
            }
            StartPipeServer();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void StartPipeServer()
    {
        new Thread(() =>
        {
            while (true)
            {
                using (NamedPipeServerStream server = new NamedPipeServerStream(PipeName, PipeDirection.In))
                {
                    server.WaitForConnection();
                    using (StreamReader reader = new StreamReader(server))
                    {
                        string? filePath = reader.ReadLine();
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            App.OnFileOpened(filePath);
                        }
                    }
                }
            }
        })
        { IsBackground = true }.Start();
    }
}