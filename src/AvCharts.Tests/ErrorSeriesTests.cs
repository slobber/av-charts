using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the series that draws an error bar: that a whisker reaches one end of the error to the other, that the
/// mark at the reading is the widest of the three pieces and a cap half as wide, that a record whose error was
/// never measured is drawn as its reading alone, and that a pointer anywhere along the bar finds it.
/// </summary>
/// <remarks>As with the other bands, the geometry is rectangles and the evidence is the ink.</remarks>
[Collection(AvaloniaCollection.Name)]
public class ErrorSeriesTests
{
    [Fact]
    public void A_bar_reaches_one_end_of_its_error_to_the_other_and_marks_the_reading_across_the_band()
    {
        // One reading worth twelve, three either side of it, on an axis of 0 to 20: the whisker runs from nine to
        // fifteen, and the cap in the middle of the slot is forty pixels wide.
        var series = Bars([(12, 3)]);

        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);
        var middle = plot.Left + (plot.Width / 2);
        var ink = Ink.Bounds(picture);

        Close(At(plot, 15), ink.Top);
        Close(At(plot, 9), ink.Bottom);
        Close(middle - 20, ink.Left);
        Close(middle + 20, ink.Right);

        // The mark runs the whole width of the band, and a cap half as far either way from the middle: a region of
        // five rows around each of them holds that piece and the whisker crossing it.
        var bar = new Rect(middle - 22, 0, 44, 400);

        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(bar.Left, At(plot, 12) - 2, bar.Width, 5)), 38, 48);
        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(bar.Left, At(plot, 15) - 2, bar.Width, 5)), 16, 24);
        Assert.InRange(Ink.Count(picture, Colors.Red, new Rect(bar.Left, At(plot, 9) - 2, bar.Width, 5)), 16, 24);

        // Between the mark and a cap there is the whisker and nothing else: six rows of it are a handful of
        // pixels, where six rows of a cap would be a second cap.
        Assert.InRange(
            Ink.Count(picture, new Rect(bar.Left, (At(plot, 15) + At(plot, 12)) / 2, bar.Width, 6)),
            1,
            8);
    }

    [Fact]
    public void A_reading_whose_doubt_was_never_measured_is_drawn_as_its_reading_alone()
    {
        // No error given at all: the mark stands where the reading is, and there is no whisker to reach anywhere.
        var series = Bars([(12, 3.0)]);

        series.ErrorSelector = null;
        series.MaxBandWidth = 40;

        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var picture = Paint(chart, series);
        var ink = Ink.Bounds(picture);

        // One row of the band's width, or two where the rasterizer splits a hairline between rows: what is being
        // asserted is that the mark is a hairline and that it is drawn at all, and which of the two rows it lands
        // on is the rasterizer's business rather than the series'.
        Assert.InRange(ink.Bottom - ink.Top, 0, 1);
        Assert.InRange(Ink.Count(picture, Colors.Red), 30, 80);
        Close(At(plot, 12), ink.Top);
    }

    [Fact]
    public void An_axis_covers_the_ends_of_the_error_rather_than_the_distance_to_zero()
    {
        // Readings a hundred units above nothing, with an error of five either side: the axis covers ninety-five
        // to a hundred and thirteen or so and is not dragged down to zero.
        var series = Bars([(100, 5), (108, 5)]);
        var chart = Chart(series, maximum: null);
        var range = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        Assert.InRange(range.Minimum, 90, 96);
        Assert.InRange(range.Maximum, 112, 120);
        Assert.True(range.Minimum > 50, $"the axis runs from {range.Minimum}, which is on its way to zero");
    }

    [Fact]
    public void A_pointer_anywhere_along_the_bar_finds_it()
    {
        // The point of the shape-aware hit test: a pointer near the top of a whisker is on that bar even though
        // the reading it stands for is three units lower down.
        var series = Bars([(12, 3), (16, 1)]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;

        Paint(chart, series);

        var hit = series.HitTestItem(new Point(plot.Left + (plot.Width / 4), At(plot, 15)), 2);

        Assert.NotNull(hit);
        Assert.Equal(0, hit!.Value.ChartItem.Index);

        // And a point below the whisker's end is not on it, nor is one out in the gutter.
        Assert.Null(series.HitTestItem(new Point(plot.Left + (plot.Width / 4), At(plot, 7)), 2));
        Assert.Null(series.HitTestItem(new Point(plot.Left - 20, At(plot, 12)), 2));
    }

    /// <summary>One reading: what it is worth, and how much either side of it was measured.</summary>
    /// <param name="Day">Which day it is.</param>
    /// <param name="Value">The reading.</param>
    /// <param name="Error">The error either side of it.</param>
    private sealed record Reading(string Day, double Value, double Error);

    /// <summary>A series of error bars over a category axis, dressed in a colour of its own.</summary>
    /// <param name="readings">The readings, in the order they are recorded.</param>
    /// <returns>The series.</returns>
    private static ErrorSeries<Reading> Bars((double Value, double Error)[] readings) =>
        new()
        {
            ItemsSource = [.. readings.Select((reading, index) => new Reading($"day{index}", reading.Value, reading.Error))],
            XSelector = reading => reading.Day,
            YSelector = reading => reading.Value,
            ErrorSelector = reading => reading.Error,
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
