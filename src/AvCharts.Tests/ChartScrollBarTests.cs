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
/// Checks the bar a chart's window is dragged by: how much of the track its thumb takes, where the thumb stands,
/// what a drag and a press on the track do to the window, and that a host can dress the whole thing.
/// </summary>
/// <remarks>
/// <para>
/// The rules are driven through the methods the pointer handlers call rather than through synthesised pointer
/// input, for the reason the gesture tests give: the headless platform's mouse helpers do not reach a window in
/// this setup, so routing is the one part of a gesture these cannot speak for.
/// </para>
/// <para>
/// What is expected is worked out from the chart rather than written down as a number: the track is the plot's own
/// stretch of the bar, because the bar keeps the room the chart leaves around its plot - so a test that assumed the
/// whole bar would be asserting the width of the gutter, which is the chart's business and moves when a label does.
/// Every assertion here is instead one of the rules: the thumb's length is the share of the readings on screen, its
/// place is that share's place among them, a drag moves the window by what the thumb moved, and a press on the
/// track puts the window where the pointer is.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ChartScrollBarTests
{
    /// <summary>The chart is laid out at this size, and the bar at the same length along the way it runs.</summary>
    private const int ChartWidth = 400;

    private const int ChartHeight = 200;
    private const double BarThickness = 8;

    /// <summary>The whole of the readings, on both dimensions.</summary>
    private static readonly double[] All = [.. Enumerable.Range(0, 101).Select(index => (double)index)];

    [Fact]
    public void The_thumb_is_as_long_as_the_share_of_the_readings_on_screen()
    {
        var (chart, bar) = Page();
        var (start, length) = Thumb(bar);

        // Readings from nothing to a hundred with a window of twenty of them: a fifth of the track is thumb.
        WithinAPixelOrTwo(Track(chart, Orientation.Horizontal) / 5, length);

        // And the track runs from where the plot starts rather than from the end of the bar: the room the chart
        // leaves for the labels down the side of the plot is the room the bar leaves, so that a thumb at the start
        // of the readings sits under the start of the plot.
        WithinAPixelOrTwo(Screen(chart, Orientation.Horizontal, 0), start);
        Assert.True(bar.CanScroll, "a fifth of the readings is on screen, so there is somewhere to scroll to");
    }

    [Fact]
    public void The_thumb_stands_where_the_window_stands_among_the_readings()
    {
        // The same window moved halfway along what it can reach: forty of the eighty units it could travel over.
        var (chart, bar) = Page(minimum: 40, maximum: 60);
        var track = Track(chart, Orientation.Horizontal);
        var (start, length) = Thumb(bar);

        WithinAPixelOrTwo(track / 5, length);
        WithinAPixelOrTwo(Screen(chart, Orientation.Horizontal, 0.5 * (track - (track / 5))), start);
        Assert.Equal(40, Window(chart, ChartDimension.X).Minimum, 3);
    }

    [Fact]
    public void Dragging_the_thumb_moves_the_window_by_what_it_moved()
    {
        var (chart, bar) = Page();
        var track = Track(chart, Orientation.Horizontal);
        var thumb = track / 5;
        var travel = track - thumb;

        // Taking hold of the thumb where it is and moving it half of its travel puts the window half of the way
        // along the readings: forty of the eighty units it can reach.
        bar.Down(new Point(Screen(chart, Orientation.Horizontal, thumb / 2), BarThickness / 2));
        bar.Move(new Point(Screen(chart, Orientation.Horizontal, (thumb / 2) + (travel / 2)), BarThickness / 2));
        bar.Up();
        Lay(chart);

        Assert.Equal(40, Window(chart, ChartDimension.X).Minimum, 3);
        Assert.Equal(60, Window(chart, ChartDimension.X).Maximum, 3);

        // The window is the same length it was - the bar moves the window rather than changing it - and the thumb
        // has come with it.
        Assert.Equal(20, Window(chart, ChartDimension.X).Length, 3);
        WithinAPixelOrTwo(Screen(chart, Orientation.Horizontal, 0.5 * travel), Thumb(bar).Start);
    }

    [Fact]
    public void A_press_on_the_track_takes_the_window_there()
    {
        var (chart, bar) = Page();
        var middle = Screen(chart, Orientation.Horizontal, Track(chart, Orientation.Horizontal) / 2);

        // Not on the thumb, so this is a reader asking to go where they pressed rather than taking hold: the window
        // ends up centred on the reading that is under the pointer, which is halfway along the readings.
        bar.Down(new Point(middle, BarThickness / 2));
        bar.Up();
        Lay(chart);

        var window = Window(chart, ChartDimension.X);

        Assert.Equal(50, (window.Minimum + window.Maximum) / 2, 3);
        Assert.Equal(20, window.Length, 3);
    }

    [Fact]
    public void A_vertical_bar_moves_the_vertical_window_and_leaves_the_other_one_alone()
    {
        var (chart, bar) = Page(Orientation.Vertical);
        var across = Window(chart, ChartDimension.X);
        var middle = Screen(chart, Orientation.Vertical, Track(chart, Orientation.Vertical) / 2);
        var thumb = Thumb(bar);

        // The track runs up the readings, so its low end is the bottom of the plot - which is where the window sits
        // while it is at the beginning of them.
        WithinAPixelOrTwo(Screen(chart, Orientation.Vertical, 0), thumb.Start + thumb.Length - 1);

        bar.Down(new Point(BarThickness / 2, middle));
        bar.Up();
        Lay(chart);

        var window = Window(chart, ChartDimension.Y);

        Assert.Equal(50, (window.Minimum + window.Maximum) / 2, 3);
        Assert.Equal(20, window.Length, 3);
        Assert.Equal(across, Window(chart, ChartDimension.X));
    }

    [Fact]
    public void A_window_on_all_of_the_readings_has_nothing_to_scroll()
    {
        var (chart, bar) = Page(minimum: 0, maximum: 100);

        Assert.False(bar.CanScroll, "the whole of the readings is on screen");

        // The thumb is as long as the track - a bar drawn solid, saying "all of it is here" - and a drag on it moves
        // nothing, because there is nothing beyond either end to move to.
        var (start, length) = Thumb(bar);

        WithinAPixelOrTwo(Screen(chart, Orientation.Horizontal, 0), start);
        WithinAPixelOrTwo(Track(chart, Orientation.Horizontal), length);

        bar.Down(new Point(Screen(chart, Orientation.Horizontal, 20), BarThickness / 2));
        bar.Move(new Point(Screen(chart, Orientation.Horizontal, 120), BarThickness / 2));
        bar.Up();
        Lay(chart);

        Assert.Equal(0, Window(chart, ChartDimension.X).Minimum, 3);
        Assert.Equal(100, Window(chart, ChartDimension.X).Maximum, 3);
    }

    [Fact]
    public void The_readings_are_what_the_bar_measures_against_and_not_the_hosts_data()
    {
        var (chart, bar) = Page();

        chart.Series.Add(Readings([.. Enumerable.Range(0, 11).Select(index => (double)index)]));

        // A second series that reaches no further does not move the track, which is the readings' own extent.
        Lay(chart);
        Lay(bar);

        WithinAPixelOrTwo(Track(chart, Orientation.Horizontal) / 5, Thumb(bar).Length);

        // Hiding the long series takes its readings out of the ranges, so the window is now wider than anything left
        // - and the bar says so rather than pretending there is somewhere to scroll to.
        chart.Series[0].Visible = false;
        Lay(chart);
        Lay(bar);

        Assert.False(bar.CanScroll, "the window now covers all of what is left");
        WithinAPixelOrTwo(Track(chart, Orientation.Horizontal), Thumb(bar).Length);

        chart.Series[0].Visible = true;
        Lay(chart);
        Lay(bar);

        Assert.True(bar.CanScroll);
        WithinAPixelOrTwo(Track(chart, Orientation.Horizontal) / 5, Thumb(bar).Length);
    }

    [Fact]
    public void A_chart_with_no_readings_has_a_track_and_no_thumb()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        Lay(chart);

        var bar = Bar(chart);

        Lay(bar);

        Assert.False(bar.CanScroll);
        Assert.Equal(0, Ink.Count(Picture(bar)));

        // And a bar pointed at nothing at all is the same picture rather than a fault.
        bar.Chart = null;

        Assert.False(bar.CanScroll);
        Assert.Equal(0, Ink.Count(Picture(bar)));
    }

    [Fact]
    public void The_theme_dresses_the_bar_and_a_host_can_dress_it_differently()
    {
        var (chart, bar, theme) = Dressed();

        Assert.Same(ThemeValue(theme, ChartTokens.ScrollbarTrack), bar.Background);
        Assert.Same(ThemeValue(theme, ChartTokens.ScrollbarThumb), bar.ThumbBrush);
        Assert.Same(ThemeValue(theme, ChartTokens.ScrollbarThumbHover), bar.ThumbHoverBrush);

        // A host's own value under the same key is what the bar wears instead, without a property of the host's page
        // changing - the promise every token makes.
        var (_, mine, _) = Dressed(new ResourceDictionary { [ChartTokens.ScrollbarThumb] = Brushes.HotPink });

        Assert.Same(Brushes.HotPink, mine.ThumbBrush);

        // The chart is along for the ride in all of this: nothing about dressing a bar changes what the chart thinks
        // its window is.
        Assert.Equal(0, Window(chart, ChartDimension.X).Minimum, 3);
    }

    /// <summary>A bar that can be driven the way its pointer handlers drive it.</summary>
    private sealed class BarProbe : ChartScrollBar
    {
        public void Down(Point at) => DragStart(at);

        public void Move(Point at) => DragMove(at);

        public void Up() => DragEnd();
    }

    /// <summary>A chart of readings with a bar beside it, both laid out, and neither of them dressed.</summary>
    /// <param name="orientation">Which way the bar runs.</param>
    /// <param name="minimum">The low end of the window on both dimensions.</param>
    /// <param name="maximum">The high end of it.</param>
    /// <param name="values">The readings; a hundred of them by default.</param>
    /// <returns>The chart and the bar.</returns>
    private static (CartesianChart Chart, BarProbe Bar) Page(
        Orientation orientation = Orientation.Horizontal,
        double minimum = 0,
        double maximum = 20,
        double[]? values = null)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = minimum, Maximum = maximum },
            VerticalAxis = new LinearAxis { Minimum = minimum, Maximum = maximum },
        };

        chart.Series.Add(Readings(values ?? All));
        Lay(chart);

        var bar = Bar(chart, orientation);

        Lay(bar);
        return (chart, bar);
    }

    /// <summary>A bar over a chart, with brushes of its own so that its thumb can be measured.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="orientation">Which way the bar runs.</param>
    /// <returns>The bar.</returns>
    private static BarProbe Bar(ChartBase? chart, Orientation orientation = Orientation.Horizontal) =>
        new()
        {
            Chart = chart,
            Orientation = orientation,
            Background = null,
            ThumbBrush = Brushes.White,
        };

    /// <summary>A page with a chart, a bar under it, and the theme dressing the two.</summary>
    /// <param name="overrides">Resources for the bar itself, as a host with a value of its own would set.</param>
    /// <returns>The chart, the bar and the theme.</returns>
    private static (CartesianChart Chart, BarProbe Bar, ResourceDictionary Theme) Dressed(
        ResourceDictionary? overrides = null)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 20 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 20 },
        };

        chart.Series.Add(Readings(All));

        var bar = new BarProbe { Chart = chart };

        if (overrides is not null)
        {
            bar.Resources = overrides;
        }

        var page = new DockPanel();

        DockPanel.SetDock(bar, Dock.Bottom);
        page.Children.Add(bar);
        page.Children.Add(chart);

        var theme = ChartTheme.Create();
        var resources = new ResourceDictionary();

        resources.MergedDictionaries.Add(theme);

        // The window is what dresses the page, the way an application's resources dress one.
        _ = new Window { Resources = resources, Content = page };

        page.Measure(new Size(ChartWidth, ChartHeight));
        page.Arrange(new Rect(0, 0, ChartWidth, ChartHeight));

        return (chart, bar, theme);
    }

    /// <summary>A series of readings, drawn wherever its values say.</summary>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Readings(double[] values) =>
        new()
        {
            ItemsSource = values,
            XSelector = value => value,
            YSelector = value => value,
        };

    /// <summary>The stretch of the bar the readings are drawn along: the plot's own room at each end of it.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="orientation">Which way the bar runs.</param>
    /// <returns>How long the track is, in pixels.</returns>
    /// <remarks>The bar is laid out as long as the chart, so the two are read in one space.</remarks>
    private static double Track(CartesianChart chart, Orientation orientation) =>
        orientation == Orientation.Horizontal ? chart.PlotBounds.Width : chart.PlotBounds.Height;

    /// <summary>Where a point a given way along the track falls on the bar, in the bar's own coordinates.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="orientation">Which way the bar runs.</param>
    /// <param name="along">How far along the readings, in pixels from their lowest value.</param>
    /// <returns>The coordinate: across for a horizontal bar, down for a vertical one.</returns>
    /// <remarks>
    /// The track is the plot's own stretch of the bar, so counting from the low end of the readings counts from the
    /// left of the plot across it and from the bottom of it up - which is the way a reader reads the two.
    /// </remarks>
    private static double Screen(CartesianChart chart, Orientation orientation, double along) =>
        orientation == Orientation.Horizontal ? chart.PlotBounds.Left + along : chart.PlotBounds.Bottom - along;

    /// <summary>Where the thumb is along the bar, read off the picture rather than worked out again here.</summary>
    /// <param name="bar">The bar.</param>
    /// <returns>Its start and its length, in pixels, from the top or the left of the bar.</returns>
    /// <remarks>
    /// The bar is painted with no track and a white thumb, so the ink in the picture is the thumb and nothing else:
    /// what a reader sees, measured the way a reader sees it.
    /// </remarks>
    private static (double Start, double Length) Thumb(ChartScrollBar bar)
    {
        var (left, top, right, bottom) = Ink.Bounds(Picture(bar));

        Assert.True(right >= left && bottom >= top, "the bar painted no thumb at all");

        return bar.Orientation == Orientation.Horizontal
            ? (left, right - left + 1)
            : (top, bottom - top + 1);
    }

    /// <summary>Checks a measurement of ink against what the geometry says, a pixel or two either way.</summary>
    /// <param name="expected">Where the edge was meant to land.</param>
    /// <param name="measured">Where the pixels landed.</param>
    /// <remarks>
    /// A rectangle filled from 23.86 to 97.90 covers the pixels 23 to 97, which is one more than its width - and one
    /// more again at each end when both edges land between pixels. The slack is the pixel grid's rather than the
    /// bar's, so every measurement of ink in this file is taken with it; what is being asserted is which share of
    /// the track the thumb covers and where it starts, not the rounding.
    /// </remarks>
    private static void WithinAPixelOrTwo(double expected, double measured) =>
        Assert.InRange(measured, expected - 2, expected + 2);

    /// <summary>The bar drawn into a picture of its own.</summary>
    /// <param name="bar">The bar.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(ChartScrollBar bar)
    {
        var picture = new RenderTargetBitmap(Box(bar));

        using (var context = picture.CreateDrawingContext())
        {
            bar.Render(context);
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

    /// <summary>Lays the bar out along the side of that chart, as long as the chart is.</summary>
    /// <param name="bar">The bar.</param>
    /// <remarks>
    /// Laid out by hand rather than by a layout pass, which is why the arrangement is let go of first: it is a pass
    /// that tells a control on a chart's page that the chart was laid out again, and this is standing in for one -
    /// so it asks for the arrangement the way the pass would.
    /// </remarks>
    private static void Lay(ChartScrollBar bar)
    {
        var box = Box(bar);
        var size = new Size(box.Width, box.Height);

        bar.InvalidateArrange();
        bar.Measure(size);
        bar.Arrange(new Rect(size));
    }

    /// <summary>The box a bar is laid out in: as long as the chart, and a bar's thickness the other way.</summary>
    /// <param name="bar">The bar.</param>
    /// <returns>The box.</returns>
    private static PixelSize Box(ChartScrollBar bar) =>
        bar.Orientation == Orientation.Horizontal
            ? new PixelSize(ChartWidth, (int)BarThickness)
            : new PixelSize((int)BarThickness, ChartHeight);

    /// <summary>What a chart's window on one dimension is, in unit space.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The range.</returns>
    private static AxisUnitRange Window(CartesianChart chart, ChartDimension dimension) =>
        chart.Transform.GetLayout(dimension).UnitRange;

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
