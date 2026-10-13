using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the shapes a series can be drawn as beyond a plain line: marks on their own, a line that steps, a
/// region filled under the line, and marks whose size is a third value.
/// </summary>
/// <remarks>
/// The evidence is the ink, as it is for the columns: one series painted on its own into a bitmap, and the
/// pixels in a chosen part of it counted. A shape is a shape even when nothing in the model describes it.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ShapeSeriesTests
{
    [Fact]
    public void A_plot_draws_marks_and_nothing_between_them()
    {
        var plot = new PlotSeries<double>
        {
            ItemsSource = [1.0, 5.0, 9.0],
            XSelector = value => value,
            YSelector = value => value,
            Marker = Markers.Circle,
            MarkerSize = 4,
            MarkerFill = Brushes.Red,
        };

        var line = new LineSeries<double>
        {
            ItemsSource = [1.0, 5.0, 9.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var chart = Chart(plot, line);

        var marks = InkCount(chart, plot);
        var joined = InkCount(chart, line);

        Assert.True(marks > 0, "the marks should have been drawn");
        Assert.True(joined > marks, $"a line between the points covers more than marks alone ({joined} against {marks})");

        // Between the first two points there is nothing at all: a plot is not a line with the ink turned down.
        var between = Between(chart, 1, 5);

        var bitmap = InkBitmap(chart, plot);

        Assert.Equal(0, Ink.Count(bitmap, between));
    }

    [Fact]
    public void A_step_line_goes_across_before_it_goes_up()
    {
        var step = new StepLineSeries<double>
        {
            ItemsSource = [1.0, 5.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var chart = Chart(step);
        var corner = new Rect(Pixel(chart, 5, 1).X - 3, Pixel(chart, 5, 1).Y - 3, 6, 6);

        // The corner above the second point: a stepped line passes through it, a straight one does not.
        Assert.True(Ink.Count(InkBitmap(chart, step), corner) > 0, "the step should corner at the second point");
    }

    [Fact]
    public void A_line_does_not_pass_through_the_corner_a_step_does()
    {
        var line = new LineSeries<double>
        {
            ItemsSource = [1.0, 5.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var chart = Chart(line);
        var corner = new Rect(Pixel(chart, 5, 1).X - 3, Pixel(chart, 5, 1).Y - 3, 6, 6);

        Assert.Equal(0, Ink.Count(InkBitmap(chart, line), corner));
    }

    [Fact]
    public void An_area_fills_under_the_line_and_stops_at_a_gap()
    {
        var values = new double[] { 2, 4, 6, double.NaN, 6, 4, 2 };
        var area = new AreaSeries<double>
        {
            ItemsSource = values,
            XSelector = value => value is double.NaN ? 3 : Array.IndexOf(values, value),
            YSelector = value => value,
            Fill = Brushes.Red,
        };

        // The horizontal values are the positions, so the gap is the middle one of seven.
        area.XSelector = null;

        var chart = Chart(area);
        var bitmap = InkBitmap(chart, area);
        var plot = chart.PlotBounds;

        // The region is filled: the middle of the plot, under the line, has ink all the way down to the floor.
        var under = new Rect(Pixel(chart, 1, 0).X - 2, plot.Bottom - 5, 4, 4);

        Assert.True(Ink.Count(bitmap, under) > 0, "the region should reach the floor of the plot");

        // And the gap is a gap: the column where the missing record is has nothing in it, because a value that
        // was never measured is not drawn as though it had been.
        var gap = new Rect(Pixel(chart, 3, 0).X - 1, plot.Top, 3, plot.Height);

        Assert.Equal(0, Ink.Count(bitmap, gap));
    }

    [Fact]
    public void A_bubble_is_a_plot_whose_marks_differ_from_one_another()
    {
        var bubble = new BubbleSeries<Bubble>
        {
            ItemsSource = [new Bubble(1, 1, 2), new Bubble(9, 1, 12)],
            XSelector = item => item.X,
            YSelector = item => item.Y,
            RadiusSelector = item => item.Radius,
            Marker = Markers.Circle,
            MarkerFill = Brushes.Red,
        };

        var chart = Chart(bubble);
        var bitmap = InkBitmap(chart, bubble);
        var small = Pixel(chart, 1, 1);
        var large = Pixel(chart, 9, 1);

        var smallInk = Ink.Count(bitmap, new Rect(small.X - 14, small.Y - 14, 28, 28));
        var largeInk = Ink.Count(bitmap, new Rect(large.X - 14, large.Y - 14, 28, 28));

        // The big mark is a disc of radius twelve and the small one of radius two, so the ink in the same square
        // of the picture is many times what the small one covers.
        Assert.True(largeInk > smallInk * 6, $"expected a much bigger mark ({largeInk} against {smallInk})");
    }

    [Fact]
    public void A_series_that_fills_is_coloured_by_its_own_fill()
    {
        var area = new AreaSeries<double> { Fill = Brushes.Red };
        var plot = new PlotSeries<double>();

        // The palette rule: a series that brought a colour takes no slot, and the colour it reports is the one it
        // is drawn in.
        Assert.True(area.HasOwnColours);
        Assert.Same(Brushes.Red, area.Color);
        Assert.False(plot.HasOwnColours);
    }

    private static CartesianChart Chart(params Series[] series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
        return chart;
    }

    /// <summary>The rectangle between two horizontal values, over the whole height of the plot.</summary>
    private static Rect Between(CartesianChart chart, double from, double to)
    {
        var plot = chart.PlotBounds;
        var left = Pixel(chart, from, 0).X;
        var right = Pixel(chart, to, 0).X;

        // Six pixels clear of each end, which is more than the reach of a mark.
        return new Rect(left + 6, plot.Top, Math.Max(0, right - left - 12), plot.Height);
    }

    private static Point Pixel(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 10 * plot.Width), plot.Bottom - (y / 10 * plot.Height));
    }

    private static int InkCount(CartesianChart chart, Series series) => Ink.Count(InkBitmap(chart, series));

    private static RenderTargetBitmap InkBitmap(CartesianChart chart, Series series)
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

    /// <summary>Something with a place and a size of its own, which is what a bubble needs.</summary>
    private sealed record Bubble(double X, double Y, double Radius);
}
