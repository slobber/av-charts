using Avalonia.Controls;
using Lucide.Avalonia;

namespace AvCharts.Demo;

/// <summary>The pages the demo has, in the groups its navigation shows them in.</summary>
/// <remarks>
/// The navigation is built from this list, so a page is one line here - and the golden tests walk the same list,
/// which is how "every page of the gallery is still drawn, and still drawn the same" is checked rather than
/// remembered.
/// </remarks>
public static class Catalog
{
    /// <summary>How wide a page is drawn, which is what the golden pictures are.</summary>
    public const int PageWidth = 640;

    /// <summary>
    /// How tall a page is drawn.
    /// </summary>
    /// <remarks>
    /// Tall enough for the longest page, which is the acceptance one: an example, a hundred thousand readings, and
    /// the playground under them. A page that outgrows this fails the check beside the golden pictures, because a
    /// chart drawn past the bottom of the picture is a chart nobody looked at.
    /// </remarks>
    public const int PageHeight = 1120;

    /// <summary>The groups, in the order they appear.</summary>
    public static IReadOnlyList<DemoGroup> Groups { get; } =
    [
        new("Charts",
        [
            new("Everything at once", "The acceptance list on one page, and a hundred thousand readings.", Pages.Acceptance, LucideIconKind.ListChecks),
            new("Lines", "A line, one with markers, an area under one, and two step lines.", Pages.Lines, LucideIconKind.ChartLine),
            new("Columns", "Columns clustered, stacked, and with a line over them.", Pages.Columns, LucideIconKind.ChartColumn),
            new("Bars", "The same kind of thing lying down, against categories.", Pages.Bars, LucideIconKind.ChartBar),
            new("Points", "A scatter of readings, and a bubble sized by a third reading.", Pages.Points, LucideIconKind.ChartScatter),
        ]),
        new("Live data",
        [
            new("Streaming", "A thousand readings a second, arriving while the chart draws.", Pages.Streaming, LucideIconKind.Activity),
            new("Oscilloscope", "A triggered sweep of a live signal, with a timebase and a gain.", Pages.Oscilloscope, LucideIconKind.AudioWaveform),
        ]),
        new("Axes and scales",
        [
            new("Axes", "Linear, logarithmic, time, duration and categorical scales.", Pages.Axes, LucideIconKind.Ruler),
            new("Two scales", "Two vertical scales on one plot, with a legend beside it.", Pages.Scales, LucideIconKind.Scale),
            new("ECG", "A fixed scale and an even grid, sampled, with gaps where the lead came off.", Pages.Ecg, LucideIconKind.HeartPulse),
        ]),
        new("Theme and interaction",
        [
            new("Theme", "One chart, in either variant and in a host's own colours.", Pages.Theming, LucideIconKind.SunMoon),
            new("Palette", "The ten colours, light on the left and dark on the right.", Pages.Palette, LucideIconKind.Palette),
            new("Interaction", "A tip for every reading, a legend, and a crosshair for the pointer.", Pages.Interaction, LucideIconKind.Crosshair),
            new("Annotations", "A threshold, a band, and a mark where something happened.", Pages.Annotations, LucideIconKind.Layers),
        ]),
    ];
}

/// <summary>A group of pages in the navigation.</summary>
/// <param name="Title">What the group is called.</param>
/// <param name="Entries">The pages in it.</param>
public sealed record DemoGroup(string Title, IReadOnlyList<DemoEntry> Entries);

/// <summary>One page: what it is called, what it shows, and how to build it.</summary>
/// <param name="Title">The name in the navigation.</param>
/// <param name="Description">One line about what is on it.</param>
/// <param name="Create">Makes the page.</param>
/// <param name="Icon">The icon its row in the navigation wears, from the house's icon set.</param>
public sealed record DemoEntry(string Title, string Description, Func<Control> Create, LucideIconKind Icon);
