using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
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
    public void Every_reading_the_scatter_page_draws_is_written_out()
    {
        // The page asks for a tip on every reading, and a page is drawn once with no pointer to hover with - so
        // every reading on it has to have one. It had one: the readings are not in the order of their horizontal
        // value, and a series that is out of order was being taken for one that is.
        var page = Show("Points");

        page.Measure(new Avalonia.Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Avalonia.Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var chart = page.GetVisualDescendants().OfType<CartesianChart>().First();
        var readings = chart.GetAllDataPoints().Count();
        var tips = chart.GetVisualDescendants().OfType<DataTip>().Where(tip => tip.IsVisible).ToArray();

        Assert.True(readings > 0, "the scatter page should be drawing readings");
        Assert.Equal(readings, tips.Length);

        // And two of them are not in the same place: a page of tips that all landed on one another reads as one
        // tip, which is what a reader reported - the readings were written out and nobody could see it.
        var places = tips
            .Select(tip => new Avalonia.Rect(Canvas.GetLeft(tip), Canvas.GetTop(tip), tip.DesiredSize.Width, tip.DesiredSize.Height))
            .ToArray();

        foreach (var place in places)
        {
            Assert.True(place.Width > 0 && place.Height > 0, "a tip was measured as nothing, so the theme did not reach it");
        }

        var covered = places
            .SelectMany((place, index) => places.Skip(index + 1).Select(other => (place, other)))
            .Count(pair => pair.place.Intersects(pair.other));

        Assert.True(
            covered == 0,
            $"{covered} pairs of the {places.Length} tips cover each other: at "
            + string.Join(", ", places.Take(3).Select(place => place.Position))
            + "; the readings are at "
            + string.Join(", ", chart.GetAllDataPoints().Take(3).Select(item => item.Position))
            + $"; the chart is {chart.Bounds}, the plot {chart.PlotBounds}");
    }

    [Fact]
    public void No_page_draws_its_readings_outside_its_plot()
    {
        // A window that does not agree with where the readings stand is a page's own mistake - and it is a
        // quiet one: the readings do not vanish, they are drawn in the axis' gutter, over the labels, where
        // they look like a rendering fault. The columns page had exactly that, because a column stands in a
        // slot reaching half a slot past each reading while the page pinned its window half a slot along.
        foreach (var entry in Catalog.Groups.SelectMany(group => group.Entries))
        {
            var page = Show(entry.Title);

            page.Measure(new Avalonia.Size(Catalog.PageWidth, Catalog.PageHeight));
            page.Arrange(new Avalonia.Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

            foreach (var chart in page.GetVisualDescendants().OfType<CartesianChart>())
            {
                var picture = new RenderTargetBitmap(new Avalonia.PixelSize(640, 720));

                using (var context = picture.CreateDrawingContext())
                {
                    chart.SeriesLayer.Render(context);
                }

                // Any ink rather than a colour: the question is where the readings ended up, not what they wear.
                var ink = Ink.Bounds(picture);
                var plot = chart.PlotBounds;

                if (ink.Right < ink.Left)
                {
                    // A chart that painted nothing has nothing to be outside of.
                    continue;
                }

                // Nothing is painted outside the plot, within a pixel of slack for a stroke's own width.
                Assert.True(
                    ink.Left >= plot.Left - 1 && ink.Right <= plot.Right + 1,
                    $"{entry.Title}: readings are drawn from {ink.Left} to {ink.Right} on a plot of {plot.Left} to {plot.Right}");

                Assert.True(
                    ink.Top >= plot.Top - 1 && ink.Bottom <= plot.Bottom + 1,
                    $"{entry.Title}: readings are drawn from {ink.Top} to {ink.Bottom} on a plot of {plot.Top} to {plot.Bottom}");
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

        // The page draws the strip of its own first, and that is the one whose paper this is about: the
        // playground further down the page has a chart with no height of its own, since its frame gives it one.
        var chart = page.GetVisualDescendants().OfType<CartesianChart>().First(one => !double.IsNaN(one.Height));
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

        // And the grid is a grid of squares: an interval across and an interval up are the same number of pixels,
        // which is read off the two axes rather than assumed - assuming it is how this test passed while the page
        // was showing rectangles, the horizontal axis left to pick a two second interval of its own.
        var across = chart.Transform.GetLayout(ChartDimension.X).UnitRange;
        var up = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;
        var horizontalCell = chart.Transform.GetLayout(ChartDimension.X).MajorStep.Length * pixelsPerSecond;
        var verticalCell = chart.Transform.GetLayout(ChartDimension.Y).MajorStep.Length * plot.Height / up.Length;

        Assert.Equal(horizontalCell, verticalCell, 0.5);
        Assert.Equal(pixelsPerSecond * (window.Length / secondsAcross), horizontalCell, 0.5);
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
