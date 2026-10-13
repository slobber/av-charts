using Avalonia;
using Avalonia.Headless;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Brings Avalonia up once for the tests that need a drawing backend.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia 12 ships Base, Controls and Markup.Xaml in one package, but a geometry is still made by the
/// platform's render interface: <c>new StreamGeometry()</c> throws without one. The tests that touch ink
/// therefore join this collection, which starts the headless platform once for all of them.
/// </para>
/// <para>
/// Skia rather than the headless drawing stub (<c>UseHeadlessDrawing = false</c>): the assertions are about
/// geometry bounds and, once the first golden image lands, about rendered pixels, and both only mean
/// something with a real backend.
/// </para>
/// </remarks>
public sealed class TestPlatform
{
    private static readonly Lock Gate = new();
    private static bool _isStarted;

    /// <summary>Starts the platform unless it is already running.</summary>
    public TestPlatform()
    {
        lock (Gate)
        {
            if (_isStarted)
            {
                return;
            }

            BuildAvaloniaApp().SetupWithoutStarting();
            _isStarted = true;
        }
    }

    /// <summary>Builds the headless application the tests run against.</summary>
    /// <returns>The builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            // A pinned font is what makes a rendered image comparable between machines: the labels are measured
            // and drawn with it, so the picture does not move because a different machine has different fonts.
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Puts every test class that needs the platform in one collection, which owns the fixture.</summary>
[CollectionDefinition(Name)]
public sealed class AvaloniaCollection : ICollectionFixture<TestPlatform>
{
    /// <summary>The collection name; use it with <c>[Collection(AvaloniaCollection.Name)]</c>.</summary>
    public const string Name = "avalonia";
}
