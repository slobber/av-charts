using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using AvCharts.Demo;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Draws every page of the gallery and compares it with the picture checked in beside the tests.
/// </summary>
/// <remarks>
/// <para>
/// The pages come out of the demo project rather than being built again here, so the picture the build checks and
/// the picture a reader sees cannot drift apart: a page that stops drawing, or starts drawing something else, is
/// a failing build rather than something someone notices a month later.
/// </para>
/// <para>
/// A golden image catches what an assertion cannot describe: a gutter that moved by two pixels, a label that grew
/// until it overlapped its neighbour, a legend that fell off the side. What it cannot tell anyone is whether the
/// picture looks right - that is what the recording is for, and why these are only written with
/// <c>AVCHARTS_UPDATE_GOLDENS=1</c>.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class GalleryGoldenTests
{
    [Theory]
    [MemberData(nameof(PageNames))]
    public void The_gallery_page(string name)
    {
        var page = Catalog.Groups
            .SelectMany(group => group.Entries)
            .Single(entry => entry.Title == name)
            .Create();

        // The page is shown the way the demo window shows it: in a window that wears the demo's styles and the
        // library's theme. That is what the page expects - a chart takes its control theme from the resources it
        // can see when it joins a tree, and a tree with no root is no tree to join.
        _ = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        GoldenImages.Verify(Name(name), page, Catalog.PageWidth, Catalog.PageHeight);
    }

    /// <summary>The names of the pages, so that each one is a test of its own with its own picture.</summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> PageNames() =>
        [.. Catalog.Groups.SelectMany(group => group.Entries).Select(entry => entry.Title)];

    /// <summary>The name of the picture a page is checked against.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The file name, without the extension.</returns>
    private static string Name(string name) => "gallery-" + name.ToLowerInvariant().Replace(' ', '-');
}
