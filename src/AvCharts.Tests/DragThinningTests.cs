using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks what a sampled series draws: one picture, worked out from the readings and kept until something it
/// depends on moves - so a window being dragged or wheeled over the readings shows the picture it shows at rest.
/// </summary>
/// <remarks>
/// The rule is the one the brief settled on for sampling - a picture is thinned only while there are more
/// readings than there are columns to draw them in, and is exact otherwise - and the thinning is kept, so a
/// window moving over the readings costs a map rather than a pass over every one of them. What this class is
/// about is that keeping it is invisible: a drag draws the same picture as the same window drawn without a
/// gesture, because what changes is what the window is over, not what the series is. The other half is that the
/// thinning is not kept a moment too long: a reading that arrives has to be in the picture.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class DragThinningTests
{
    /// <summary>The colour the series is drawn in, so that a test can find the line and not the furniture.</summary>
    private static readonly Color Line = Color.FromRgb(0xE0, 0x7B, 0x39);

    [Fact]
    public void A_drag_draws_the_same_picture_as_the_same_window_at_rest()
    {
        var dragged = Chart(100_000, SamplingMode.MinMax);
        dragged.Probe.DragStartAt(new Point(100, 100));
        dragged.Probe.DragTo(new Point(220, 100));
        var during = Picture(dragged.Probe);

        var atRest = Chart(100_000, SamplingMode.MinMax);
        atRest.Probe.PanBy(120, 0);
        var drawn = Picture(atRest.Probe);

        Assert.True(Ink.Count(during, Line) > 0, "the drag drew nothing");
        Assert.Equal(0, Ink.Differences(during, drawn));
    }

    [Fact]
    public void A_wheel_zoom_draws_the_same_picture_as_the_same_window_at_rest()
    {
        // A wheel moves the window the way a drag does, and it is no more a reason for the picture to differ
        // than a drag is.
        var wheeled = Chart(100_000, SamplingMode.MinMax);
        wheeled.Probe.Wheel(1, new Point(200, 100), KeyModifiers.None);
        var during = Picture(wheeled.Probe);

        var atRest = Chart(100_000, SamplingMode.MinMax);
        atRest.Probe.Zoom(1.2, new Point(200, 100));

        Assert.True(Ink.Count(during, Line) > 0, "the zoom drew nothing");
        Assert.Equal(0, Ink.Differences(during, Picture(atRest.Probe)));
    }

    [Theory]
    [InlineData(SamplingMode.None)]
    [InlineData(SamplingMode.Average)]
    [InlineData(SamplingMode.Lttb)]
    public void A_drag_of_any_other_mode_draws_the_same_picture_too(SamplingMode sampling)
    {
        var chart = Chart(100_000, sampling);
        chart.Probe.DragStartAt(new Point(100, 100));
        chart.Probe.DragTo(new Point(220, 100));
        var during = Picture(chart.Probe);

        var atRest = Chart(100_000, sampling);
        atRest.Probe.PanBy(120, 0);

        Assert.Equal(0, Ink.Differences(during, Picture(atRest.Probe)));
    }

    [Fact]
    public void Readings_that_fit_the_columns_are_drawn_as_they_are()
    {
        // Four hundred readings cannot fill six hundred columns, so there was never anything to thin.
        var chart = Chart(400, SamplingMode.MinMax);
        chart.Probe.DragStartAt(new Point(100, 100));
        chart.Probe.DragTo(new Point(160, 100));
        var during = Picture(chart.Probe);

        var atRest = Chart(400, SamplingMode.MinMax);
        atRest.Probe.PanBy(60, 0);

        Assert.Equal(0, Ink.Differences(during, Picture(atRest.Probe)));
    }

    [Fact]
    public void A_reading_that_changes_is_in_the_picture()
    {
        // What keeping the thinning costs: it has to be worked out again when the readings move, or the chart
        // goes on drawing what it drew a moment ago. One reading in the middle of the series is raised above
        // anything the signal holds - the sine runs from ten to ninety - so the ink has to reach higher than it
        // did. A reading in the middle, because that leaves the window where it was: what is being checked is
        // the thinning, not the axis.
        var readings = Readings(100_000);
        var chart = Chart(readings, SamplingMode.MinMax);
        var before = Highest(Picture(chart.Probe));

        readings[500] = 95;
        chart.Probe.Series[0].InvalidateData();
        var after = Highest(Picture(chart.Probe));

        Assert.True(after < before, $"the reading that changed is not in the picture: {after} against {before}");
    }

    /// <summary>The highest row of the line's ink, which is where a peak shows.</summary>
    /// <param name="picture">The picture.</param>
    /// <returns>The row; the picture's height when there is no ink at all.</returns>
    private static int Highest(RenderTargetBitmap picture)
    {
        var highest = picture.PixelSize.Height;

        for (var column = 0; column < picture.PixelSize.Width; column++)
        {
            var (top, _) = Ink.Column(picture, column, Line);

            if (top >= 0 && top < highest)
            {
                highest = top;
            }
        }

        return highest;
    }

    /// <summary>The readings a chart is drawn from: a shape with peaks and troughs in it.</summary>
    /// <param name="count">How many readings.</param>
    /// <returns>The list, which the caller may keep adding to.</returns>
    private static List<double> Readings(int count)
    {
        var readings = new List<double>(count);

        for (var index = 0; index < count; index++)
        {
            readings.Add(50 + (40 * Math.Sin(index / 50.0)));
        }

        return readings;
    }

    /// <summary>A chart of readings, dressed and laid out, with a probe over it so a test can drive a gesture.</summary>
    /// <param name="count">How many readings.</param>
    /// <param name="sampling">How the series thins them.</param>
    /// <returns>The chart.</returns>
    private static (ChartProbe Probe, CartesianChart Chart) Chart(int count, SamplingMode sampling) =>
        Chart(Readings(count), sampling);

    /// <summary>The same, over readings a test already has.</summary>
    /// <param name="readings">The readings.</param>
    /// <param name="sampling">How the series thins them.</param>
    /// <returns>The chart.</returns>
    private static (ChartProbe Probe, CartesianChart Chart) Chart(List<double> readings, SamplingMode sampling)
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
        };

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = readings,
            YSelector = value => value,
            Stroke = new SolidColorBrush(Line),
            StrokeThickness = 1.5,
            Sampling = sampling,
        });

        chart.Measure(new Size(680, 320));
        chart.Arrange(new Rect(0, 0, 680, 320));

        return (chart, chart);
    }

    /// <summary>Paints the chart into a bitmap, which is what a frame puts on screen.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(680, 320));
        bitmap.Render(chart);
        return bitmap;
    }

    /// <summary>A chart whose gestures are a test's to drive, the way the pointer handlers would drive them.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Wheel(double notches, Point at, KeyModifiers modifiers) => WheelZoom(notches, at, modifiers);

        public void Zoom(double factor, Point at) => ZoomBy(factor, at);

        public void DragStartAt(Point at) => DragStart(at);

        public bool DragTo(Point at) => DragMove(at, KeyModifiers.None);
    }
}
