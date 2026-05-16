using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Hosting;

namespace Cratebase.LocalAgent;

public sealed class LocalAgentApplication : Avalonia.Application
{
    public static IHost? Host { get; set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new Window
            {
                Title = "Cratebase Local Agent",
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual
            };
            desktop.Exit += (_, _) => Host?.Dispose();
        }

        if (Host is null)
        {
            throw new InvalidOperationException("Local agent host is not configured");
        }

        Host.Start();
        base.OnFrameworkInitializationCompleted();
    }
}
