using Avalonia;
using Avalonia.Headless;

namespace AvCharts.Benchmarks;

/// <summary>
/// Brings the headless drawing platform up once, the way the tests do, so that a benchmark which draws is
/// measured through a real renderer.
/// </summary>
internal static class Headless
{
    private static readonly Lock Gate = new();
    private static bool _isStarted;

    /// <summary>Starts the platform unless it is already running.</summary>
    public static void Start()
    {
        lock (Gate)
        {
            if (_isStarted)
            {
                return;
            }

            AppBuilder.Configure<Application>()
                .WithInterFont()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();

            _isStarted = true;
        }
    }
}
