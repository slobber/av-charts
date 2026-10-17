using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the overview: that it draws the whole of the readings however little of them is on screen, that the
/// window it draws over them is the part that is on screen, and what taking hold of that window does.
/// </summary>
/// <remarks>
/// The rules are driven through the methods the pointer handlers call rather than through synthesised pointer
/// input, for the reason the gesture tests give. What is asserted is what a reader sees: the ink of the shape, and
/// the ink of the window - each measured by its own colour, so one can be read without the other in the way.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ChartOverviewTests
{
    /// <summary>The chart is laid out at this size, and the overview at the same length along the way it runs.</summary>
    private const int ChartWidth = 400;

    private const int ChartHeight = 200;
    private const double Thickness = 40;

    /// <summary>The whole of the readings, on both dimensions.</summary>
    private static readonly double[] All = [.. Enumerable.Range(0, 101).Select(index => (double)index)];

    /// <summary>The colour the window is painted in when a test wants to look at it and nothing else.</summary>
    private static readonly ISolidColorBrush Mark = Brushes.Lime;

    [Fact]
    public void The_whole_recording_is_drawn_however_little_of_it_is_on_screen()
    {
        var (chart, overview, _) = Page();
        var (left, _, right, _) = Ink.Bounds(Picture(overview));

        // A fifth of the readings is on screen, and the shape runs the whole of the track: what an overview is for
        // is showing what is *not* on screen, so a picture that stopped where the window does would be no use.
        WithinAPixelOrTwo(PlotStart(chart, Orientation.Horizontal), left);
        WithinAPixelOrTwo(PlotStart(chart, Orientation.Horizontal) + Track(chart, Orientation.Horizontal), right);

        // And it is the readings' own shape: the values run from the corner to the opposite one, so the ink of the
        // line reaches the top and the bottom of the panel.
        var (from, top, _, bottom) = Ink.Bounds(Picture(overview));

        Assert.Equal(0.0, (double)top, 1);
        Assert.Equal(Thickness - 1, bottom, 1);
        Assert.Equal(left, from);
    }

    [Fact]
    public void The_shape_is_drawn_in_the_colour_the_chart_gave_the_series()
    {
        var (_, overview, theme) = Page();
        var drawn = Colour(theme, ChartTokens.SeriesStroke(1));

        Assert.True(
            Ink.Count(Picture(overview), drawn) > 0,
            "the shape is not drawn in the colour the chart gave the series");
    }

    [Fact]
    public void The_window_is_the_share_of_the_readings_that_is_on_screen()
    {
        var (chart, overview, _) = Page();

        // Only the window is painted, in a colour of its own, so the ink of it can be measured on its own.
        overview.WindowFill = Mark;
        overview.WindowPen = null;

        var track = Track(chart, Orientation.Horizontal);
        var (left, _, right, _) = Ink.Bounds(Picture(overview), Mark.Color, new Rect(0, 0, ChartWidth, Thickness));

        // A fifth of the readings is on screen and the window starts where they do, so it covers a fifth of the
        // track from the end the track starts at - which is the plot's own left edge.
        WithinAPixelOrTwo(PlotStart(chart, Orientation.Horizontal), left);
        WithinAPixelOrTwo(PlotStart(chart, Orientation.Horizontal) + (track / 5), right);
    }

    [Fact]
    public void Dragging_the_window_moves_it_by_what_it_moved()
    {
        var (chart, overview, _) = Page();
        var readings = 100d;
        var window = 20d;

        // Taking the window by its middle and moving it half of the way it can travel: forty of the eighty units.
        overview.Down(new Point(Screen(chart, Orientation.Horizontal, window / 2), Thickness / 2));
        overview.Move(new Point(
            Screen(chart, Orientation.Horizontal, (window / 2) + ((readings - window) / 2)),
            Thickness / 2));
        overview.Up();
        Lay(chart);

        Assert.Equal(40, Window(chart, ChartDimension.X).Minimum, 3);
        Assert.Equal(60, Window(chart, ChartDimension.X).Maximum, 3);
        Assert.Equal(window, Window(chart, ChartDimension.X).Length, 3);
    }

    [Fact]
    public void Dragging_an_end_stretches_the_window()
    {
        var (chart, overview, _) = Page();

        // Taking hold of the end the window finishes at and dragging it to the middle of the readings: the window
        // grows from where it starts, which is where its other end still is.
        overview.Down(new Point(Screen(chart, Orientation.Horizontal, 20), Thickness / 2));
        overview.Move(new Point(Screen(chart, Orientation.Horizontal, 50), Thickness / 2));
        overview.Up();
        Lay(chart);

        Assert.Equal(0, Window(chart, ChartDimension.X).Minimum, 3);
        Assert.Equal(50, Window(chart, ChartDimension.X).Maximum, 3);
    }

    [Fact]
    public void A_press_away_from_the_window_puts_it_there()
    {
        var (chart, overview, _) = Page();

        // Not on the window, so this is a reader picking a stretch rather than taking hold: the window ends up
        // centred on the reading that is under the pointer, which is halfway along the readings.
        overview.Down(new Point(Screen(chart, Orientation.Horizontal, 50), Thickness / 2));
        overview.Up();
        Lay(chart);

        var window = Window(chart, ChartDimension.X);

        Assert.Equal(50, (window.Minimum + window.Maximum) / 2, 3);
        Assert.Equal(20, window.Length, 3);
    }

    [Fact]
    public void A_vertical_overview_stretches_the_vertical_window_and_leaves_the_other_one_alone()
    {
        var (chart, overview, _) = Page(Orientation.Vertical);
        var across = Window(chart, ChartDimension.X);

        overview.Down(new Point(Thickness / 2, Screen(chart, Orientation.Vertical, 20)));
        overview.Move(new Point(Thickness / 2, Screen(chart, Orientation.Vertical, 50)));
        overview.Up();
        Lay(chart);

        Assert.Equal(0, Window(chart, ChartDimension.Y).Minimum, 3);
        Assert.Equal(50, Window(chart, ChartDimension.Y).Maximum, 3);
        Assert.Equal(across, Window(chart, ChartDimension.X));
    }

    [Fact]
    public void A_window_on_all_of_the_readings_has_nothing_to_pick()
    {
        var (chart, overview, _) = Page(minimum: 0, maximum: 100);

        Assert.False(overview.CanScroll, "the whole of the readings is on screen");

        overview.WindowFill = Mark;
        overview.WindowPen = null;

        var track = Track(chart, Orientation.Horizontal);
        var (left, _, right, _) = Ink.Bounds(Picture(overview), Mark.Color, new Rect(0, 0, ChartWidth, Thickness));

        WithinAPixelOrTwo(PlotStart(chart, Orientation.Horizontal), left);
        WithinAPixelOrTwo(PlotStart(chart, Orientation.Horizontal) + track, right);
    }

    [Fact]
    public void The_theme_dresses_the_overview_and_a_host_can_dress_it_differently()
    {
        var (_, overview, theme) = Page(bare: false);

        Assert.Same(ThemeValue(theme, ChartTokens.OverviewBackground), overview.Background);
        Assert.Same(ThemeValue(theme, ChartTokens.OverviewWindowFill), overview.WindowFill);
        Assert.Same(ThemeValue(theme, ChartTokens.OverviewWindowStroke), overview.WindowPen);

        var (_, mine, _) = Page(
            overrides: new ResourceDictionary { [ChartTokens.OverviewWindowFill] = Brushes.HotPink });

        Assert.Same(Brushes.HotPink, mine.WindowFill);
    }

    /// <summary>An overview that can be driven the way its pointer handlers drive it.</summary>
    private sealed class OverviewProbe : ChartOverview
    {
        public void Down(Point at) => DragStart(at);

        public void Move(Point at) => DragMove(at);

        public void Up() => DragEnd();
    }

    /// <summary>A chart of readings with an overview beside it, both laid out, dressed by the theme.</summary>
    /// <param name="orientation">Which way the overview runs.</param>
    /// <param name="minimum">The low end of the window on both dimensions.</param>
    /// <param name="maximum">The high end of it.</param>
    /// <param name="overrides">Resources for the overview itself, as a host with a value of its own would set.</param>
    /// <param name="bare">
    /// Whether to leave the panel unpainted, which is what the tests that measure ink need: a panel filling the
    /// whole of the picture is ink too, and it would swallow the shape's.
    /// </param>
    /// <returns>The chart, the overview and the theme.</returns>
    private static (CartesianChart Chart, OverviewProbe Overview, ResourceDictionary Theme) Page(
        Orientation orientation = Orientation.Horizontal,
        double minimum = 0,
        double maximum = 20,
        ResourceDictionary? overrides = null,
        bool bare = true)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = minimum, Maximum = maximum },
            VerticalAxis = new LinearAxis { Minimum = minimum, Maximum = maximum },
        };

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = All,
            XSelector = value => value,
            YSelector = value => value,
        });

        var overview = new OverviewProbe
        {
            Chart = chart,
            Orientation = orientation,
        };

        if (bare)
        {
            // The panel is left unpainted, so that the ink in the picture it draws is the shape's and nothing else.
            overview.Background = null;
        }

        if (overrides is not null)
        {
            overview.Resources = overrides;
        }

        var page = new DockPanel();

        DockPanel.SetDock(overview, orientation == Orientation.Horizontal ? Dock.Bottom : Dock.Right);
        page.Children.Add(overview);
        page.Children.Add(chart);

        var theme = ChartTheme.Create();
        var resources = new ResourceDictionary();

        resources.MergedDictionaries.Add(theme);

        // The window is what dresses the page, the way an application's resources dress one.
        _ = new Window { Resources = resources, Content = page };

        Lay(chart);
        Lay(overview);

        return (chart, overview, theme);
    }

    /// <summary>The stretch of the overview the readings are drawn along: the plot's own room at each end of it.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="orientation">Which way the overview runs.</param>
    /// <returns>How long the track is, in pixels.</returns>
    private static double Track(CartesianChart chart, Orientation orientation) =>
        orientation == Orientation.Horizontal ? chart.PlotBounds.Width : chart.PlotBounds.Height;

    /// <summary>Where the track starts, in the overview's own coordinates.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="orientation">Which way the overview runs.</param>
    /// <returns>The coordinate the track starts at.</returns>
    private static double PlotStart(CartesianChart chart, Orientation orientation) =>
        orientation == Orientation.Horizontal ? chart.PlotBounds.Left : chart.PlotBounds.Bottom;

    /// <summary>Where a value on one dimension falls on the overview, in its own coordinates.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="orientation">Which way the overview runs.</param>
    /// <param name="value">The value, on readings that run from nothing to a hundred.</param>
    /// <returns>The coordinate: across for a horizontal overview, down for a vertical one.</returns>
    /// <remarks>
    /// The track is the whole of the readings, so a value's place among them is its place along the track - counted
    /// from the low end, which is the left of the plot across and the bottom of it up.
    /// </remarks>
    private static double Screen(CartesianChart chart, Orientation orientation, double value) =>
        orientation == Orientation.Horizontal
            ? chart.PlotBounds.Left + (value / 100 * chart.PlotBounds.Width)
            : chart.PlotBounds.Bottom - (value / 100 * chart.PlotBounds.Height);

    /// <summary>Checks a measurement of ink against what the geometry says, a pixel or two either way.</summary>
    /// <param name="expected">Where the edge was meant to land.</param>
    /// <param name="measured">Where the pixels landed.</param>
    private static void WithinAPixelOrTwo(double expected, double measured) =>
        Assert.InRange(measured, expected - 2, expected + 2);

    /// <summary>The overview drawn into a picture of its own.</summary>
    /// <param name="overview">The overview.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(ChartOverview overview)
    {
        var picture = new RenderTargetBitmap(Box(overview));

        using (var context = picture.CreateDrawingContext())
        {
            overview.Render(context);
        }

        return picture;
    }

    /// <summary>Lays a chart out at the size the page gives one.</summary>
    /// <param name="chart">The chart.</param>
    private static void Lay(CartesianChart chart)
    {
        chart.Measure(new Size(ChartWidth, ChartHeight));
        chart.Arrange(new Rect(0, 0, ChartWidth, ChartHeight));
    }

    /// <summary>Lays the overview out along the side of that chart, as long as the chart is.</summary>
    /// <param name="overview">The overview.</param>
    private static void Lay(ChartOverview overview)
    {
        var box = Box(overview);
        var size = new Size(box.Width, box.Height);

        overview.Measure(size);
        overview.Arrange(new Rect(size));
    }

    /// <summary>The box an overview is laid out in: as long as the chart, and a panel's thickness the other way.</summary>
    /// <param name="overview">The overview.</param>
    /// <returns>The box.</returns>
    private static PixelSize Box(ChartOverview overview) =>
        overview.Orientation == Orientation.Horizontal
            ? new PixelSize(ChartWidth, (int)Thickness)
            : new PixelSize((int)Thickness, ChartHeight);

    /// <summary>What a chart's window on one dimension is, in unit space.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The range.</returns>
    private static AxisUnitRange Window(CartesianChart chart, ChartDimension dimension) =>
        chart.Transform.GetLayout(dimension).UnitRange;

    /// <summary>One token of the theme as the colour it is, in the light variant.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token's key.</param>
    /// <returns>The colour.</returns>
    private static Color Colour(ResourceDictionary theme, string token)
    {
        Assert.True(theme.TryGetResource(token, ThemeVariant.Light, out var value), $"{token} is not defined");
        return Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsAssignableFrom<Pen>(value).Brush).Color;
    }

    /// <summary>A value out of the theme, in the light variant.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token's name.</param>
    /// <returns>The value.</returns>
    private static object ThemeValue(ResourceDictionary theme, string token)
    {
        Assert.True(theme.TryGetResource(token, ThemeVariant.Light, out var value), $"{token} is not defined");
        return value!;
    }
}
