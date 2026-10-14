using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the curve a line can be drawn with: that it goes through every reading, that it stays between the
/// readings either side of it, and that leaving it alone draws the straight line it always did.
/// </summary>
/// <remarks>
/// The assertions are about ink rather than about the geometry, because what a reader gets is ink: where the
/// curve is on the picture is the question, and a curve that overshoots is a peak the instrument never recorded.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class CurveTests
{
    [Fact]
    public void A_line_is_straight_unless_it_is_asked_to_be_curved()
    {
        var series = Line([0.0, 6.0, 0.0]);

        Assert.Equal(CurveMode.Linear, series.Curve);

        var chart = Chart(series);

        // A third of the way up the first climb, which is where a curve and a straight line part company: the
        // curve leaves the reading below it rising, so it is above the straight line the whole way to the top.
        var below = chart.DataToPoint(ChartValue.FromNumber(0.5), ChartValue.FromNumber(3.4))!.Value;
        var above = chart.DataToPoint(ChartValue.FromNumber(0.5), ChartValue.FromNumber(5.5))!.Value;
        var over = new Rect(below.X - 4, above.Y, 8, below.Y - above.Y);

        Assert.Equal(0, Ink.Count(Picture(chart), Colors.OrangeRed, over));

        series.Curve = CurveMode.Smooth;

        Assert.True(Ink.Count(Picture(chart), Colors.OrangeRed, over) > 0, "the curve should have risen over the line");
    }

    [Fact]
    public void A_curve_goes_through_every_reading()
    {
        // A reading is where the curve has to be: it is the only thing that was measured.
        var series = Line([1.0, 7.0, 3.0, 9.0, 5.0]);

        series.Curve = CurveMode.Smooth;

        var chart = Chart(series);
        var bitmap = Picture(chart);

        foreach (var item in series.Items)
        {
            var at = item.Position!.Value;
            var around = new Rect(at.X - 2, at.Y - 2, 4, 4);

            Assert.True(
                Ink.Count(bitmap, Colors.OrangeRed, around) > 0,
                $"nothing is drawn at the reading for {item.X.Number} at {at}");
        }
    }

    [Fact]
    public void A_curve_never_goes_past_the_readings_either_side_of_it()
    {
        // The whole reason for a monotone fit: a spline through readings overshoots, and the overshoot is a peak
        // or a trough that no instrument recorded. This one stays between the readings it joins - so with a
        // series that comes back up, the band above the tallest reading is empty.
        var series = Line([2.0, 9.0, 1.0, 8.0, 2.0]);

        series.Curve = CurveMode.Smooth;

        var chart = Chart(series);
        var bitmap = Picture(chart);
        var plot = chart.PlotBounds;
        var tallest = series.Items.Min(item => item.Position!.Value.Y);

        Assert.Equal(
            0,
            Ink.Count(bitmap, Colors.OrangeRed, new Rect(plot.Left, plot.Top, plot.Width, tallest - plot.Top)));
    }

    [Theory]
    [InlineData(new[] { 4.0, 4.0 })]
    [InlineData(new[] { 4.0 })]
    [InlineData(new[] { 4.0, 4.0, 4.0, 4.0 })]
    public void A_curve_of_nothing_to_curve_draws_what_a_line_would(double[] values)
    {
        // Two readings, one reading, or a flat run: the tangents are all the same number and there is nothing to
        // round off. Nothing here may come out as not-a-number and take the whole geometry with it.
        var series = Line(values);

        series.Curve = CurveMode.Smooth;

        var chart = Chart(series);
        var bitmap = Picture(chart);
        var plot = chart.PlotBounds;
        var ink = Ink.Bounds(bitmap, Colors.OrangeRed, Paint);

        Assert.True(ink.Right >= ink.Left, "nothing was drawn at all");
        Assert.True(new Rect(0, 0, 400, 300).Contains(new Rect(ink.Left, ink.Top, ink.Right - ink.Left, ink.Bottom - ink.Top)), $"the ink ran to {ink}");
        Assert.True(plot.Width > 0);
    }

    [Fact]
    public void Readings_out_of_order_are_joined_by_straight_lines()
    {
        // A monotone fit is defined where x increases. Read backwards, the honest thing is the straight line -
        // arranging someone else's data into an order it is not in would be worse.
        var series = new LineSeries<double>
        {
            ItemsSource = [5.0, 4.0, 3.0, 2.0, 1.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.OrangeRed,
            Curve = CurveMode.Smooth,
        };

        var chart = Chart(series);
        var bitmap = Picture(chart);
        var plot = chart.PlotBounds;
        var ink = Ink.Bounds(bitmap, Colors.OrangeRed, Paint);

        Assert.True(ink.Right - ink.Left > plot.Width / 2, $"the line runs {ink}");
        Assert.True(new Rect(0, 0, 400, 300).Contains(new Rect(ink.Left, ink.Top, ink.Right - ink.Left, ink.Bottom - ink.Top)), $"the ink ran to {ink}");
    }

    [Fact]
    public void The_area_under_a_curve_follows_it()
    {
        // An area is the curve with the space under it filled: its top edge is the curve, so the filled shape
        // stops where the line is rather than cutting across it.
        var series = new AreaSeries<double>
        {
            ItemsSource = [1.0, 8.0, 2.0],
            YSelector = value => value,
            Stroke = Brushes.OrangeRed,
            Fill = new SolidColorBrush(Colors.Magenta),
            Curve = CurveMode.Smooth,
        };

        var chart = Chart(series);
        var bitmap = Picture(chart);
        var plot = chart.PlotBounds;
        var tallest = series.Items.Min(item => item.Position!.Value.Y);

        // Nothing is filled above the curve, and the area is filled below it.
        Assert.Equal(0, Ink.Count(bitmap, Colors.Magenta, new Rect(plot.Left, plot.Top, plot.Width, tallest - plot.Top - 1)));
        Assert.True(Ink.Count(bitmap, Colors.Magenta, new Rect(plot.Left, tallest + 4, plot.Width, plot.Bottom - tallest - 4)) > 0);
    }

    /// <summary>A line of readings, drawn straight unless a test asks otherwise.</summary>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(double[] values) =>
        new()
        {
            ItemsSource = values,
            YSelector = value => value,
            Stroke = Brushes.OrangeRed,
            StrokeThickness = 2,
        };

    /// <summary>A chart with one series on it, laid out.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(Series series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 6 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        chart.Series.Add(series);
        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));
        return chart;
    }

    /// <summary>Draws the chart, so that a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 300));
        bitmap.Render(chart);
        return bitmap;
    }

    /// <summary>The whole picture, for a claim about where ink is rather than what colour it is.</summary>
    private static readonly Rect Paint = new(0, 0, 400, 300);
}
