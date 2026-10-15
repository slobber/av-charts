using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the series that draw a price bar: that a candle stands in a band of its own, that it reaches from its
/// low to its high rather than from the bottom of the chart, and that a pointer finds the one it is over.
/// </summary>
/// <remarks>
/// As with a column, the geometry is a rectangle and the evidence is the ink: the series is painted on its own
/// into a bitmap and what it drew is measured.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class CandlestickSeriesTests
{
    [Fact]
    public void A_candle_stands_in_its_band_and_reaches_from_its_low_to_its_high()
    {
        var series = Candles([(10, 13, 8, 11), (11, 14, 9, 12.0)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var ink = InkOf(chart, series);

        // Two readings, so a band is half the plot and a candle takes 0.65 of it: the ink runs from the left edge
        // of the first to the right edge of the last, which is a band and one candle.
        var band = plot.Width / 2;

        Close(band * 1.65, ink.Right - ink.Left);

        // The highest price of the two is 14 and the lowest is 8, on an axis of 0 to 20: the ink reaches one and
        // the other, and no further.
        Close(At(plot, 14), ink.Top);
        Close(At(plot, 8), ink.Bottom);
    }

    [Fact]
    public void A_candle_is_solid_between_its_prices_and_hangs_a_hairline_off_each_end()
    {
        // A candle is filled between the price it opened at and the price it closed at, and what reaches from
        // there to the high and to the low is a hairline. Both halves of that are about two rectangles of
        // different widths, and giving them the same width draws a body with a hole in the middle of it - which
        // is what the gallery's picture of a month of candles showed, and the reason this test exists.
        var series = Candles([(10, 14, 8, 13.0)]);

        series.MaxBandWidth = 20;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);

        // One reading, so its band is the whole plot and the candle stands in the middle of it at the cap's
        // twenty pixels.
        var candle = new Rect(plot.Left + (plot.Width / 2) - 10, 0, 20, 400);

        // The body reaches from 10 to 13 on an axis of 0 to 20, and every pixel of it - two in from the edge, so
        // that the antialiasing of the outline is not what is being counted - carries the series' colour.
        var top = At(plot, 13);
        var bottom = At(plot, 10);
        var inside = new Rect(candle.Left + 2, top + 3, candle.Width - 4, (bottom - top) - 6);
        var area = (int)(inside.Width * inside.Height);

        Assert.True(inside.Height > 10, $"the body is {inside.Height} px tall, which is too small to measure");
        Assert.True(
            Ink.Count(picture, Colors.Red, inside) >= area,
            $"{Ink.Count(picture, Colors.Red, inside)} of the body's {area} pixels carry its colour, so something "
            + "has taken a hole out of the middle of it");

        // And the wick is a hairline: half way between the high and the top of the body there is nothing else to
        // see, so a row with one or two pixels of ink in it is a line and a row with twenty is a second body.
        Assert.InRange(Ink.Count(picture, new Rect(candle.Left, (At(plot, 14) + top) / 2, candle.Width, 1)), 1, 5);

        // And the same at the other end, between the bottom of the body and the low.
        Assert.InRange(Ink.Count(picture, new Rect(candle.Left, (bottom + At(plot, 8)) / 2, candle.Width, 1)), 1, 5);
    }

    [Fact]
    public void An_axis_of_prices_covers_what_the_candles_reach_rather_than_the_distance_to_zero()
    {
        // Prices a hundred units above nothing: a column would drag the axis down to zero, and a candle does not.
        var series = Candles([(100, 105, 95, 102), (102, 112, 101, 110.0)]);
        var chart = Chart(series, maximum: null);
        var range = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        Assert.InRange(range.Minimum, 85, 95);
        Assert.InRange(range.Maximum, 112, 120);
        Assert.True(range.Minimum > 50, $"the axis runs from {range.Minimum}, which is on its way to zero");
    }

    [Fact]
    public void A_pointer_finds_the_candle_it_is_over()
    {
        var series = Candles([(10, 13, 8, 11), (11, 14, 9, 12.0)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;

        Paint(chart, series);

        // The middle of the first band, which is where a reader would point at the first candle.
        var hit = series.HitTestItem(new Point(plot.X + (plot.Width / 4), plot.Bottom - (plot.Height / 2)), 4);

        Assert.NotNull(hit);
        Assert.Equal(0, hit!.Value.ChartItem.Index);

        // A point out in the gutter is not on it.
        Assert.Null(series.HitTestItem(new Point(plot.X - 20, plot.Bottom - (plot.Height / 2)), 4));
    }

    /// <summary>One bar of a price series.</summary>
    [Fact]
    public void The_theme_says_which_way_a_candle_went_and_the_series_says_no_more()
    {
        // The two colours are tokens of the theme's, handed to the series the way the palette is: a chart of
        // candles rising and falling says which is which with colour, and a host that reskins its application
        // recolours them all at once.
        var plain = Candles([(10, 13, 8, 11.0)]);
        var dressed = Candles([(10, 13, 8, 11.0)]);

        plain.RiseFill = null;
        plain.FallFill = null;
        plain.RiseStroke = null;
        plain.FallStroke = null;

        var chart = Chart([plain, dressed]);
        var theme = ChartTheme.Create();
        var window = new Window { Resources = new ResourceDictionary { MergedDictionaries = { theme } }, Content = chart };

        _ = window;
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        Assert.Same(Token(theme, ChartTokens.FinancialUp), plain.RiseFill);
        Assert.Same(Token(theme, ChartTokens.FinancialDown), plain.FallFill);

        // And a series the host dressed itself keeps what it was given.
        Assert.Same(Brushes.Red, dressed.RiseFill);
    }

    /// <summary>A colour out of the theme, in the light variant.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token's name.</param>
    /// <returns>The value.</returns>
    private static object Token(ResourceDictionary theme, string token)
    {
        Assert.True(theme.TryGetResource(token, ThemeVariant.Light, out var value), $"{token} is not defined");
        return value!;
    }

    /// <param name="Day">Which day it is.</param>
    /// <param name="Open">Where it opened.</param>
    /// <param name="High">The highest it reached.</param>
    /// <param name="Low">The lowest it reached.</param>
    /// <param name="Close">Where it closed.</param>
    private sealed record Bar(string Day, double Open, double High, double Low, double Close);

    /// <summary>A series of candles over a category axis, dressed in a colour of its own.</summary>
    /// <param name="bars">The bars, in the order they are recorded.</param>
    /// <returns>The series.</returns>
    private static CandlestickSeries<Bar> Candles((double Open, double High, double Low, double Close)[] bars) =>
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
    private static CartesianChart Chart(Series series, double? maximum = 20) => Chart([series], maximum);

    /// <summary>A chart of several series, measured and arranged.</summary>
    /// <param name="series">The series on the chart.</param>
    /// <param name="maximum">The top of the value axis; null for an axis that covers the readings.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(Series[] series, double? maximum = 20)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = maximum is { } top ? new LinearAxis { Minimum = 0, Maximum = top } : new LinearAxis(),
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
        return chart;
    }

    /// <summary>Where a price is on the chart, in the chart's own coordinates.</summary>
    /// <param name="plot">The plot.</param>
    /// <param name="price">The price, on an axis of 0 to 20.</param>
    /// <returns>The position.</returns>
    private static double At(Rect plot, double price) => plot.Bottom - (price / 20 * plot.Height);

    /// <summary>The ink one series leaves, painted on its own with the chart's own context.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="series">The series.</param>
    /// <returns>The bounds of what it drew.</returns>
    private static (int Left, int Top, int Right, int Bottom) InkOf(CartesianChart chart, Series series) =>
        Ink.Bounds(Paint(chart, series));

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
