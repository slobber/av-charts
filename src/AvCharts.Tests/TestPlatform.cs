using Avalonia;
using Avalonia.Headless;
using AvCharts.Demo;
using iPaas.Controls;
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

            // The demo's own theme, because the pages are made of ordinary controls: a picker and a switch need
            // one, exactly as they would in a host's application. The theme the library ships dresses charts;
            // it has nothing to say about a combo box.
            if (Application.Current is { } application)
            {
                application.Styles.Add(new Theme());
            }

            // The clock is pinned for the same reason the font is: the streaming page stamps its readings with
            // the time of day, and a picture that depends on when the build ran is not one the build can compare.
            // Nothing but the tests pins it - the demo reads the real clock.
            DemoClock.Now = () => new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.FromHours(8));

            _isStarted = true;
        }
    }

    /// <summary>Builds the headless application the tests run against.</summary>
    /// <returns>The builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            // A pinned font is what makes a rendered image comparable between machines: the labels are measured
            // and drawn with it, so the picture does not move because a different machine has different fonts. The
            // gallery's own two fonts, so that a test can ask whether the Chinese it writes can be drawn at all -
            // which is the one thing the pictures cannot say, since every page in them is in English.
            .WithGalleryFonts()
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
