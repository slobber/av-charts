using Avalonia;

namespace AvCharts.Demo.Desktop;

/// <summary>The gallery on the desktop: the same application, in a window.</summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Kept public and named this way because the Avalonia previewer looks for exactly this.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
