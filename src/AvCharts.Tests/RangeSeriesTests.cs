using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the series that draws a range: that a band stands in its own slot and reaches from one of its readings
/// to the other, that the axis covers the two rather than the distance to zero, and that a record with one end
/// given is drawn from that end to its own value.
/// </summary>
/// <remarks>As with a column, the geometry is a rectangle and the evidence is the ink.</remarks>
[Collection(AvaloniaCollection.Name)]
public class RangeSeriesTests
{
    [Fact]
    public void A_band_stands_in_its_slot_and_reaches_from_one_of_its_readings_to_the_other()
    {
        // One reading, so its slot is the whole plot; the cap puts the band in the middle of it at forty pixels
        // wide, and the band reaches from the low to the high and no further.
        var series = Ranges([(8, 14, 11)]);

        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);
        var middle = plot.Left + (plot.Width / 2);
        var ink = Ink.Bounds(picture);

        Close(middle - 20, ink.Left);
        Close(middle + 20, ink.Right);
        Close(At(plot, 14), ink.Top);
        Close(At(plot, 8), ink.Bottom);

        // And it is a band rather than its outline: every pixel well inside it carries the series' colour.
        var inside = new Rect(middle - 15, At(plot, 13) + 3, 30, (At(plot, 9) - At(plot, 13)) - 6);
        var area = (int)(inside.Width * inside.Height);

        Assert.True(inside.Height > 10, $"the band is {inside.Height} px tall, which is too small to measure");
        Assert.True(
            Ink.Count(picture, Colors.Red, inside) >= area * 9 / 10,
            $"{Ink.Count(picture, Colors.Red, inside)} of the band's {area} pixels carry its colour");
    }

    [Fact]
    public void A_record_with_one_end_given_reaches_from_that_end_to_its_own_value()
    {
        // The reading a record is worth is the end left open: given only the other one, the band is drawn from it
        // to the value - which is what makes a range series usable over a series that already has its readings.
        var series = Ranges([(8, 12, 14)]);

        series.HighSelector = null;
        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var ink = Ink.Bounds(Paint(chart, series));

        // The record is worth fourteen, so the band runs from the eight it was given up to fourteen rather than
        // up to the twelve it was not.
        Close(At(plot, 8), ink.Bottom);
        Close(At(plot, 14), ink.Top);
    }

    [Fact]
    public void An_axis_of_intervals_covers_the_intervals_rather_than_the_distance_to_zero()
    {
        // Intervals a hundred units above nothing: a column would drag the axis down to zero, and a band of two
        // readings does not.
        var series = Ranges([(95, 105, 100), (101, 112, 106)]);
        var chart = Chart(series, maximum: null);
        var range = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        Assert.InRange(range.Minimum, 85, 95);
        Assert.InRange(range.Maximum, 112, 120);
        Assert.True(range.Minimum > 50, $"the axis runs from {range.Minimum}, which is on its way to zero");
    }

    [Fact]
    public void A_band_is_drawn_from_its_two_readings_when_the_record_is_worth_nothing_of_its_own()
    {
        // A record of samples counted by date is a date and two numbers, and nothing else: a series that waited
        // for a value of its own before it drew would draw nothing at all, which is what the gallery's statistics
        // page found - its first cell was empty.
        var series = new RangeSeries<Interval>
        {
            ItemsSource = [new Interval("day0", 8, 14, double.NaN)],
            XSelector = interval => interval.Day,
            LowSelector = interval => interval.Low,
            HighSelector = interval => interval.High,
            Fill = Brushes.Red,
            MaxBandWidth = 40,
        };

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var ink = Ink.Bounds(Paint(chart, series));
        var middle = plot.Left + (plot.Width / 2);

        Close(middle - 20, ink.Left);
        Close(middle + 20, ink.Right);
        Close(At(plot, 14), ink.Top);
        Close(At(plot, 8), ink.Bottom);
    }

    [Fact]
    public void A_pointer_inside_the_band_finds_it_even_where_its_own_value_is_not()
    {
        // The band is the shape, so the finger that lands anywhere inside it lands on the record - the value the
        // record stands for is one end of the interval and may be nowhere near the middle of it.
        var series = Ranges([(8, 14, 11), (9, 15, 12)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;

        Paint(chart, series);

        var hit = series.HitTestItem(new Point(plot.Left + (plot.Width / 4), At(plot, 8)), 2);

        Assert.NotNull(hit);
        Assert.Equal(0, hit!.Value.ChartItem.Index);

        // And one below the band, or out in the gutter, is not.
        Assert.Null(series.HitTestItem(new Point(plot.Left + (plot.Width / 4), At(plot, 6)), 2));
        Assert.Null(series.HitTestItem(new Point(plot.Left - 20, At(plot, 11)), 2));
    }

    /// <summary>One interval: where it starts, where it ends, and what the reading itself is worth.</summary>
    /// <param name="Day">Which day it is.</param>
    /// <param name="Low">The low end.</param>
    /// <param name="High">The high end.</param>
    /// <param name="Value">What the record is worth, which is the end a band leaves open.</param>
    private sealed record Interval(string Day, double Low, double High, double Value);

    /// <summary>A series of bands over a category axis, dressed in a colour of its own.</summary>
    /// <param name="bands">The intervals, in the order they are recorded.</param>
    /// <returns>The series.</returns>
    private static RangeSeries<Interval> Ranges((double Low, double High, double Value)[] bands) =>
        new()
        {
            // Nothing about a band is drawn from the record's own value while both ends of it are given, and that
            // value is what a band falls back on when one of them is not.
            ItemsSource =
            [
                .. bands.Select((band, index) => new Interval($"day{index}", band.Low, band.High, band.Value)),
            ],
            XSelector = interval => interval.Day,
            YSelector = interval => interval.Value,
            LowSelector = interval => interval.Low,
            HighSelector = interval => interval.High,
            Fill = Brushes.Red,
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

    /// <summary>Where a value is on the chart, in the chart's own coordinates.</summary>
    /// <param name="plot">The plot.</param>
    /// <param name="value">The value, on an axis of 0 to 20.</param>
    /// <returns>The position.</returns>
    private static double At(Rect plot, double value) => plot.Bottom - (value / 20 * plot.Height);

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
