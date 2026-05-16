using Avalonia;
using Microsoft.Extensions.Hosting;

namespace Cratebase.LocalAgent;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using IHost host = LocalAgentWebHost.Create(args);
        LocalAgentApplication.Host = host;
        _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<LocalAgentApplication>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
