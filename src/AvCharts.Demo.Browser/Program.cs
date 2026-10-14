using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using AvCharts.Demo;

/// <summary>The gallery in a browser, for the published site.</summary>
/// <remarks>
/// Nothing here but the platform: the application, its theme and its pages are the desktop demo's own, which
/// is why the whole head is a dozen lines. What differs on this platform is where the gallery goes - there is
/// one view and no window, which <see cref="App"/> answers.
/// </remarks>
internal sealed partial class Program
{
    // The attribute is the truth: this head runs in a browser and nowhere else, which is what the analyser is
    // asking about when it sees the browser-only startup call.
    [SupportedOSPlatform("browser")]
    private static Task Main(string[] args) =>
        BuildAvaloniaApp().WithInterFont().StartBrowserAppAsync("out");

    /// <summary>The application, as the previewer and the browser runtime both want to find it.</summary>
    /// <returns>The builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
}
