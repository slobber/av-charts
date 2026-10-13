using Avalonia.Controls;

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

    /// <summary>How tall a page is drawn.</summary>
    public const int PageHeight = 460;

    /// <summary>The groups, in the order they appear.</summary>
    public static IReadOnlyList<DemoGroup> Groups { get; } =
    [
        new("Charts",
        [
            new("Lines", "A line, one with markers, an area under one, and two step lines.", Pages.Lines),
            new("Columns", "Columns clustered, stacked, and with a line over them.", Pages.Columns),
            new("Bars", "The same kind of thing lying down, against categories.", Pages.Bars),
            new("Points", "A scatter of readings, and a bubble sized by a third reading.", Pages.Points),
        ]),
        new("Axes and scales",
        [
            new("Axes", "Linear, logarithmic, time, duration and categorical scales.", Pages.Axes),
            new("Two scales", "Two vertical scales on one plot, with a legend beside it.", Pages.Scales),
            new("ECG", "A fixed scale and an even grid, sampled, with gaps where the lead came off.", Pages.Ecg),
        ]),
        new("Theme and interaction",
        [
            new("Palette", "The ten colours, light on the left and dark on the right.", Pages.Palette),
            new("Interaction", "A tip for every reading, a legend, and a crosshair for the pointer.", Pages.Interaction),
            new("Annotations", "A threshold, a band, and a mark where something happened.", Pages.Annotations),
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
public sealed record DemoEntry(string Title, string Description, Func<Control> Create);
