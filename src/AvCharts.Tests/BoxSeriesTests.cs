using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the series that draws a box: that the box reaches from one quartile to the other, that the whiskers
/// reach the smallest and the largest reading, that the median is a line across the box, and that a record given
/// nothing but its reading is drawn as a hairline rather than nothing at all.
/// </summary>
/// <remarks>As with the other bands, the geometry is rectangles and the evidence is the ink.</remarks>
[Collection(AvaloniaCollection.Name)]
public class BoxSeriesTests
{
    [Fact]
    public void A_box_reaches_from_one_quartile_to_the_other_and_its_whiskers_to_the_ends()
    {
        // One record of readings from two to eighteen, with its middle half from six to fourteen and its middle
        // at ten; the cap puts the box in the middle of its slot at forty pixels wide.
        var series = Boxes([(2, 6, 10, 14, 18)]);

        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);
        var middle = plot.Left + (plot.Width / 2);
        var ink = Ink.Bounds(picture);

        // The widest thing drawn is the box, and the whiskers reach the two ends.
        Close(middle - 20, ink.Left);
        Close(middle + 20, ink.Right);
        Close(At(plot, 18), ink.Top);
        Close(At(plot, 2), ink.Bottom);

        // The box is filled: six rows of it are six times forty pixels of colour, and the median across it is the
        // same. Between the top of the box and the largest reading there is the whisker and nothing else.
        var band = new Rect(middle - 20, 0, 40, 400);

        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(band.Left, At(plot, 12), band.Width, 6)), 200, 260);
        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(band.Left, At(plot, 10) - 2, band.Width, 5)), 180, 220);
        Assert.InRange(Ink.Count(picture, new Rect(band.Left, (At(plot, 18) + At(plot, 14)) / 2, band.Width, 6)), 1, 8);
    }

    [Fact]
    public void A_record_given_nothing_but_its_reading_is_a_hairline_across_the_band()
    {
        // No quartiles, no ends: everything falls back to the median, which is the reading itself, so the box
        // collapses onto it. A reading that has not been summarised yet is still a reading.
        var series = Boxes([(2, 6, 10, 14, 18)]);

        series.MinimumSelector = null;
        series.LowerQuartileSelector = null;
        series.UpperQuartileSelector = null;
        series.MaximumSelector = null;
        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var ink = Ink.Bounds(Paint(chart, series));

        Assert.InRange(ink.Bottom - ink.Top, 1, 3);
        Close(At(plot, 10), ink.Top);
        Close(plot.Left + (plot.Width / 2) + 20, ink.Right);
    }

    [Fact]
    public void A_pointer_along_the_whisker_finds_the_box()
    {
        var series = Boxes([(2, 6, 10, 14, 18), (3, 7, 11, 15, 19)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;

        Paint(chart, series);

        var hit = series.HitTestItem(new Point(plot.Left + (plot.Width / 4), At(plot, 2)), 2);

        Assert.NotNull(hit);
        Assert.Equal(0, hit!.Value.ChartItem.Index);

        Assert.Null(series.HitTestItem(new Point(plot.Left + (plot.Width / 4), At(plot, 1)), 2));
    }

    /// <summary>One record: the five readings a box is drawn from.</summary>
    /// <param name="Day">Which day it is.</param>
    /// <param name="Minimum">The smallest reading.</param>
    /// <param name="Lower">The lower quartile.</param>
    /// <param name="Median">The median.</param>
    /// <param name="Upper">The upper quartile.</param>
    /// <param name="Maximum">The largest reading.</param>
    private sealed record Summary(
        string Day,
        double Minimum,
        double Lower,
        double Median,
        double Upper,
        double Maximum);

    /// <summary>A series of boxes over a category axis, dressed in a colour of its own.</summary>
    /// <param name="records">The five readings of each record, in the order they are recorded.</param>
    /// <returns>The series.</returns>
    private static BoxSeries<Summary> Boxes((double Minimum, double Lower, double Median, double Upper, double Maximum)[] records) =>
        new()
        {
            ItemsSource =
            [
                .. records.Select((one, index) =>
                    new Summary($"day{index}", one.Minimum, one.Lower, one.Median, one.Upper, one.Maximum)),
            ],
            XSelector = summary => summary.Day,
            MinimumSelector = summary => summary.Minimum,
            LowerQuartileSelector = summary => summary.Lower,
            MedianSelector = summary => summary.Median,
            UpperQuartileSelector = summary => summary.Upper,
            MaximumSelector = summary => summary.Maximum,
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
