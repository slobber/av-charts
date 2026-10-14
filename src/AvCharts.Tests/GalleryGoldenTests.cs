using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using AvCharts.Charts;
using AvCharts.Controls;
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
        var page = Show(name);

        GoldenImages.Verify(Name(name), page, Catalog.PageWidth, Catalog.PageHeight);
    }

    /// <summary>The names of the pages, so that each one is a test of its own with its own picture.</summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> PageNames() =>
        [.. Catalog.Groups.SelectMany(group => group.Entries).Select(entry => entry.Title)];

    [Theory]
    [MemberData(nameof(PageNames))]
    public void The_charts_on_a_page_do_not_sit_on_top_of_each_other(string name)
    {
        // A page of several charts is a grid of them, and a chart measured with no height to work with will take
        // its own - spilling out of its cell and over the next row. A picture catches that only if someone looks
        // at it, so this is the assertion: no two charts on a page cover each other, and none of them is outside
        // the page.
        var page = Show(name);

        page.Measure(new Avalonia.Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Avalonia.Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var charts = page.GetVisualDescendants().OfType<CartesianChart>().ToArray();

        // In the page's own coordinates rather than in each chart's parent's: a chart in a cell of a grid is at
        // the corner of its cell, and comparing those corners would say every page had overlapping charts.
        var bounds = charts.Select(chart => InPage(chart, page)).ToArray();

        Assert.NotEmpty(charts);

        foreach (var (chart, box) in charts.Zip(bounds))
        {
            Assert.True(
                new Avalonia.Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight).Contains(box),
                $"{name}: a chart sits at {box}, off a {Catalog.PageWidth}x{Catalog.PageHeight} page");
        }

        for (var first = 0; first < bounds.Length; first++)
        {
            for (var second = first + 1; second < bounds.Length; second++)
            {
                Assert.False(
                    bounds[first].Intersects(bounds[second]),
                    $"{name}: two charts cover each other: {bounds[first]} and {bounds[second]}");
            }
        }
    }

    [Fact]
    public void The_strip_chart_is_a_grid_of_squares_with_a_curve_across_it()
    {
        // The ECG page is the one page whose whole point is a paper-like grid and a curve read on it: a fixed
        // scale across, an even one up, and the two agreeing on what a small square is. It used to draw its whole
        // curve in one pixel column, because the reading it carries is a plain number and the selector was asked
        // for "index / 200" of it - so this asserts the outcome: the curve spans the plot, and the squares are
        // square.
        var page = Show("ECG");

        page.Measure(new Avalonia.Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Avalonia.Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var chart = page.GetVisualDescendants().OfType<CartesianChart>().Single();
        var plot = chart.PlotBounds;
        var horizontal = (LinearAxis)chart.HorizontalAxis;

        var pixelsPerSecond = horizontal.PixelsPerUnit;
        var secondsAcross = plot.Width / pixelsPerSecond;
        var window = chart.Transform.GetLayout(AvCharts.Charts.ChartDimension.X).UnitRange;

        // What is drawn runs across the whole plot rather than sitting in one column.
        var drawn = chart.Series[0].Items.ToArray();

        Assert.NotEmpty(drawn);
        Assert.True(
            drawn[^1].Position!.Value.X > plot.Right - 2,
            $"the last reading in view is at {drawn[^1].Position!.Value.X}, and the plot ends at {plot.Right}");

        // And a small square of the grid is the same number of pixels either way: a fifth of a second across is
        // one of those squares, as is a fifth of the vertical interval.
        var horizontalSquare = pixelsPerSecond * 0.2;
        var verticalSquare = plot.Height / 3 * 0.1;

        Assert.Equal(horizontalSquare, verticalSquare, 0.2);
        Assert.Equal(8, horizontalSquare, 0.5);
        Assert.True(window.Length >= secondsAcross - 0.5, "the fixed scale should show what its width allows");
    }

    /// <summary>Where a visual sits on the page, rather than where it sits inside its own parent.</summary>
    /// <param name="visual">The visual.</param>
    /// <param name="page">The control the coordinates are wanted in.</param>
    /// <returns>The rectangle, in the page's own coordinates.</returns>
    private static Avalonia.Rect InPage(Visual visual, Control page)
    {
        var origin = new Avalonia.Point();

        for (Visual? node = visual; node is not null && !ReferenceEquals(node, page); node = node.GetVisualParent())
        {
            origin += node.Bounds.Position;
        }

        return new Avalonia.Rect(origin, visual.Bounds.Size);
    }
    /// <summary>Shows a page the way the demo window does, so that its charts are dressed and measured.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The page.</returns>
    /// <remarks>
    /// The page goes into a window that wears the demo's styles and the library's theme, which is what a page
    /// expects: a chart takes its control theme from the resources it can see when it joins a tree, and a tree
    /// with no root is no tree to join.
    /// </remarks>
    private static Control Show(string name)
    {
        var page = Catalog.Groups
            .SelectMany(group => group.Entries)
            .Single(entry => entry.Title == name)
            .Create();

        _ = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        return page;
    }

    /// <summary>The name of the picture a page is checked against.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The file name, without the extension.</returns>
    private static string Name(string name) => "gallery-" + name.ToLowerInvariant().Replace(' ', '-');
}
