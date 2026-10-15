using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the series that draws a price bar as a high-low-close bar: that it hangs a hairline from the low to the
/// high, that a tick reaches half a band each way from the middle at the open and at the close, that the colour
/// says which way the price went, and that a pointer finds the bar it is over.
/// </summary>
/// <remarks>
/// As with a candle, the geometry is a set of rectangles and the evidence is the ink: the series is painted on its
/// own into a bitmap and what it drew is measured.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class HighLowCloseSeriesTests
{
    [Fact]
    public void A_bar_hangs_a_hairline_from_its_low_to_its_high_and_a_tick_each_way_from_the_middle()
    {
        // One reading, so its band is the whole plot; the cap puts the bar in the middle of it at forty pixels
        // wide, and a tick reaches half of that from the middle either way - Flex's w2 = width / 2.
        var series = Bars([(10, 14, 8, 13.0)]);

        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);
        var middle = plot.Left + (plot.Width / 2);

        // The two ticks are what makes the bar as wide as the band it was given, and the line reaches the prices.
        var ink = Ink.Bounds(picture);

        Close(middle - 20, ink.Left);
        Close(middle + 20, ink.Right);
        Close(At(plot, 14), ink.Top);
        Close(At(plot, 8), ink.Bottom);

        // A tick is half the bar: the open tick reaches from the left edge of the band to the middle column and
        // the close tick from there to the right edge - Flex's w2 = width / 2 either way. The band of five rows
        // looked at around each of them holds that tick and the hairline crossing it, and nothing of the other.
        var bar = new Rect(middle - 22, 0, 44, 400);

        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(bar.Left, At(plot, 10) - 2, 22, 5)), 20, 28);
        Assert.Equal(0, Ink.Count(picture, Colors.Red, new Rect(middle + 1, At(plot, 10) - 2, 21, 5)));

        Assert.Equal(0, Ink.Count(picture, Colors.Red, new Rect(bar.Left, At(plot, 13) - 2, 21, 5)));
        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(middle, At(plot, 13) - 2, 22, 5)), 20, 28);

        // Between the ticks there is the hairline and nothing else: six rows of it are six pixels, where six rows
        // of a tick would be a second tick.
        Assert.InRange(
            Ink.Count(picture, new Rect(bar.Left, (At(plot, 14) + At(plot, 10)) / 2, bar.Width, 6)),
            1,
            8);

        // And the same between the close and the low.
        Assert.InRange(
            Ink.Count(picture, new Rect(bar.Left, (At(plot, 13) + At(plot, 8)) / 2, bar.Width, 6)),
            1,
            8);
    }

    [Fact]
    public void An_axis_of_prices_covers_what_the_bars_reach_rather_than_the_distance_to_zero()
    {
        // Prices a hundred units above nothing: a column would drag the axis down to zero, and a bar of prices
        // does not - it reaches from its low to its high and leaves the axis to cover those.
        var series = Bars([(100, 105, 95, 102.0), (102, 112, 101, 110.0)]);
        var chart = Chart(series, maximum: null);
        var range = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        Assert.InRange(range.Minimum, 85, 95);
        Assert.InRange(range.Maximum, 112, 120);
        Assert.True(range.Minimum > 50, $"the axis runs from {range.Minimum}, which is on its way to zero");
    }

    [Fact]
    public void The_colour_says_which_way_the_price_went_over_the_whole_bar()
    {
        // The first bar closed above where it opened and the second below it: the two are drawn in the two
        // colours, and each of them is drawn whole - Flex's high-low-close bar has one colour for the lot.
        var series = Bars([(10, 13, 8, 12.0), (13, 15, 11, 11.0)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);

        var left = new Rect(plot.Left, 0, plot.Width / 2, 400);
        var right = new Rect(plot.Left + (plot.Width / 2), 0, plot.Width / 2, 400);

        Assert.True(Ink.Count(picture, Colors.Red, left) > 20, "the first bar rose and is not drawn in red");
        Assert.True(Ink.Count(picture, Colors.Blue, right) > 20, "the second bar fell and is not drawn in blue");

        Assert.Equal(0, Ink.Count(picture, Colors.Blue, left));
        Assert.Equal(0, Ink.Count(picture, Colors.Red, right));
    }

    [Fact]
    public void A_bar_dressed_in_a_pen_is_drawn_in_the_colour_of_that_pen()
    {
        // A bar is three hairlines and has no inside to fill, so a host who sets a pen - which is how a candle is
        // outlined - would otherwise get the fill's colour or the palette's. The pen's own brush is what is read.
        var series = Bars([(10, 13, 8, 12.0)]);

        series.RiseStroke = new Pen(Brushes.Green, 2);

        var chart = Chart(series);
        var picture = Paint(chart, series);

        Assert.True(Ink.Count(picture, Colors.Green) > 20, "the bar was not drawn in the pen's colour");
        Assert.Equal(0, Ink.Count(picture, Colors.Red));
    }

    [Fact]
    public void A_pointer_finds_the_bar_it_is_over()
    {
        var series = Bars([(10, 13, 8, 12.0), (13, 15, 11, 11.0)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;

        Paint(chart, series);

        // The middle of the second band, which is where a reader would point at the second bar.
        var hit = series.HitTestItem(new Point(plot.X + (plot.Width * 0.75), plot.Bottom - (plot.Height / 2)), 4);

        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Value.ChartItem.Index);

        // A point out in the gutter is not on it.
        Assert.Null(series.HitTestItem(new Point(plot.X - 20, plot.Bottom - (plot.Height / 2)), 4));
    }

    [Fact]
    public void A_bar_given_nothing_but_a_close_is_a_cross_rather_than_nothing_at_all()
    {
        // A price a feed reports one number of is still a price: with no open, high or low the bar collapses onto
        // the close - two ticks and a line a pixel long - rather than disappearing.
        var series = Bars([(10, 14, 8, 13.0)]);

        series.OpenSelector = null;
        series.HighSelector = null;
        series.LowSelector = null;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);
        var ink = Ink.Bounds(picture);

        Assert.True(ink.Right > ink.Left, "the bar drew nothing at all");
        Assert.InRange(ink.Bottom - ink.Top, 1, 3);
        Close(At(plot, 13), ink.Top);
    }

    /// <param name="Day">Which day it is.</param>
    /// <param name="Open">Where it opened.</param>
    /// <param name="High">The highest it reached.</param>
    /// <param name="Low">The lowest it reached.</param>
    /// <param name="Close">Where it closed.</param>
    private sealed record Bar(string Day, double Open, double High, double Low, double Close);

    /// <summary>A series of high-low-close bars over a category axis, dressed in colours of its own.</summary>
    /// <param name="bars">The bars, in the order they are recorded.</param>
    /// <returns>The series.</returns>
    private static HighLowCloseSeries<Bar> Bars((double Open, double High, double Low, double Close)[] bars) =>
        new()
        {
            ItemsSource = [.. bars.Select((bar, index) => new Bar($"day{index}", bar.Open, bar.High, bar.Low, bar.Close))],
            XSelector = bar => bar.Day,
            OpenSelector = bar => bar.Open,
            HighSelector = bar => bar.High,
            LowSelector = bar => bar.Low,
            CloseSelector = bar => bar.Close,
            RiseFill = Brushes.Red,
            FallFill = Brushes.Blue,
        };

    /// <summary>A chart of one series, measured and arranged.</summary>
    /// <param name="series">The series on the chart.</param>
    /// <param name="maximum">The top of the value axis; null for an axis that covers the readings.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(Series series, double? maximum = 20)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = maximum is { } top ? new LinearAxis { Minimum = 0, Maximum = top } : new LinearAxis(),
        };

        chart.Series.Add(series);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
        return chart;
    }

    /// <summary>Where a price is on the chart, in the chart's own coordinates.</summary>
    /// <param name="plot">The plot.</param>
    /// <param name="price">The price, on an axis of 0 to 20.</param>
    /// <returns>The position.</returns>
    private static double At(Rect plot, double price) => plot.Bottom - (price / 20 * plot.Height);

    /// <summary>Paints one series, so that it has been told where its band stands.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="series">The series.</param>
    /// <returns>What it drew.</returns>
    private static RenderTargetBitmap Paint(CartesianChart chart, Series series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(600, 400));

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }

    /// <summary>Asserts that what was measured is what the geometry should have drawn, within a pixel or two.</summary>
    /// <param name="expected">What the arithmetic says.</param>
    /// <param name="actual">What the bitmap holds.</param>
    private static void Close(double expected, double actual) =>
        Assert.True(
            Math.Abs(expected - actual) <= 2,
            $"expected about {expected}, measured {actual}");
}
