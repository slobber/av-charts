using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks what a drag draws: readings that are not moving may be drawn from a thinning the series kept, and
/// letting go of the window has to put the exact picture back.
/// </summary>
/// <remarks>
/// The rule is the one the brief settled on for sampling - a picture is thinned only while there are more
/// readings than there are columns to draw them in, and is exact otherwise - extended to the frames of a drag:
/// the window moving changes the picture without changing the readings, and drawing it from the thinning the
/// last frame worked out is what keeps a drag of a hundred thousand readings inside a frame.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class DragThinningTests
{
    /// <summary>The colour the series is drawn in, so that a test can find the line and not the furniture.</summary>
    private static readonly Color Line = Color.FromRgb(0xE0, 0x7B, 0x39);

    /// <summary>How far a column's outline may have moved and still be the same picture.</summary>
    private const int AllowedDrift = 2;

    [Fact]
    public void A_drag_draws_the_same_picture_as_drawing_the_readings_exactly()
    {
        var dragged = Chart(100_000, SamplingMode.MinMax);
        dragged.Probe.SeriesLayer.IsDragging = true;
        dragged.Probe.PanBy(120, 0);
        var provisional = Picture(dragged.Probe);

        var exact = Chart(100_000, SamplingMode.MinMax);
        exact.Probe.PanBy(120, 0);
        var drawn = Picture(exact.Probe);

        // Nothing at all would be wrong the other way: the provisional picture has to have ink in it.
        Assert.True(Ink.Count(provisional, Line) > 0, "the drag drew nothing");
        Assert.True(
            WorstDrift(provisional, drawn) <= AllowedDrift,
            $"a drag moved the outline by {WorstDrift(provisional, drawn)} pixels against the exact picture");
    }

    [Fact]
    public void Letting_go_of_a_drag_puts_the_exact_picture_back()
    {
        var chart = Chart(100_000, SamplingMode.MinMax);
        chart.Probe.SeriesLayer.IsDragging = true;
        chart.Probe.PanBy(120, 0);
        _ = Picture(chart.Probe);

        chart.Probe.SeriesLayer.IsDragging = false;
        var after = Picture(chart.Probe);

        var exact = Chart(100_000, SamplingMode.MinMax);
        exact.Probe.PanBy(120, 0);

        // Pixel for pixel, not nearly: what a drag drew is thrown away when it ends.
        Assert.Equal(0, Ink.Differences(after, Picture(exact.Probe)));
    }

    [Fact]
    public void A_real_drag_says_so_and_letting_go_puts_the_exact_picture_back()
    {
        var chart = Chart(100_000, SamplingMode.MinMax);

        chart.Probe.Down(new Point(100, 100));
        Assert.True(chart.Probe.Move(new Point(220, 100), KeyModifiers.None), "the drag should have panned the chart");
        Assert.True(chart.Probe.SeriesLayer.IsDragging, "a drag that panned should have said it was dragging");
        _ = Picture(chart.Probe);

        chart.Probe.Up();
        Assert.False(chart.Probe.SeriesLayer.IsDragging, "letting go should have said the drag was over");
        var after = Picture(chart.Probe);

        var exact = Chart(100_000, SamplingMode.MinMax);
        exact.Probe.PanBy(120, 0);

        Assert.Equal(0, Ink.Differences(after, Picture(exact.Probe)));
    }

    [Theory]
    [InlineData(SamplingMode.None)]
    [InlineData(SamplingMode.Average)]
    [InlineData(SamplingMode.Lttb)]
    public void Only_a_min_max_thinning_is_kept_for_a_drag(SamplingMode sampling)
    {
        // The thinning kept for a drag is a min/max one, so that is the only mode with something kept for a drag
        // to draw from: the others work their picture out every frame, drag or no drag - which is what these say.
        var chart = Chart(100_000, sampling);
        chart.Probe.SeriesLayer.IsDragging = true;
        chart.Probe.PanBy(120, 0);
        var dragged = Picture(chart.Probe);

        var exact = Chart(100_000, sampling);
        exact.Probe.PanBy(120, 0);

        Assert.Equal(0, Ink.Differences(dragged, Picture(exact.Probe)));
    }

    [Fact]
    public void A_drag_of_readings_that_fit_the_columns_is_drawn_exactly()
    {
        // Four hundred readings cannot fill six hundred columns, so there was never anything to thin - and a
        // drag is not a reason to start drawing an approximation of a picture that was exact.
        var chart = Chart(400, SamplingMode.MinMax);
        chart.Probe.SeriesLayer.IsDragging = true;
        chart.Probe.PanBy(60, 0);
        var dragged = Picture(chart.Probe);

        var exact = Chart(400, SamplingMode.MinMax);
        exact.Probe.PanBy(60, 0);

        Assert.Equal(0, Ink.Differences(dragged, Picture(exact.Probe)));
    }

    [Fact]
    public void A_wheel_zoom_is_drawn_from_the_kept_thinning_and_settles_back_to_the_exact_picture()
    {
        // A wheel moves the window the way a drag does, so it gets the same trade: while the wheel is being
        // turned the frames come from the thinning the series keeps, and the exact picture is back once it
        // stops. Measured on a hundred thousand readings in a browser: without this, every notch worked the
        // thinning out again from every reading, which is seconds of work a notch rather than a frame.
        var chart = Chart(100_000, SamplingMode.MinMax);

        chart.Probe.Wheel(1, new Point(200, 100), KeyModifiers.None);

        Assert.True(chart.Probe.SeriesLayer.IsDragging, "a wheel that zoomed did not say the window was moving");
        var provisional = Picture(chart.Probe);

        // The wheel has nothing to let go of, so the settle is what ends the gesture - and the same zoom drawn
        // without it is what the settled picture has to be, pixel for pixel.
        chart.Probe.Settle();
        var settled = Picture(chart.Probe);

        var exact = Chart(100_000, SamplingMode.MinMax);
        exact.Probe.Wheel(1, new Point(200, 100), KeyModifiers.None);
        exact.Probe.Settle();
        var drawn = Picture(exact.Probe);

        Assert.False(chart.Probe.SeriesLayer.IsDragging, "settling should have said the window was still");
        Assert.True(Ink.Count(provisional, Line) > 0, "the zoom drew nothing");
        Assert.True(
            WorstDrift(provisional, drawn) <= AllowedDrift,
            $"a zoom moved the outline by {WorstDrift(provisional, drawn)} pixels against the exact picture");
        Assert.Equal(0, Ink.Differences(settled, drawn));
    }

    /// <summary>A chart of readings, dressed and laid out, with a probe over it so a test can drag it.</summary>
    /// <param name="count">How many readings.</param>
    /// <param name="sampling">How the series thins them.</param>
    /// <returns>The chart.</returns>
    private static (ChartProbe Probe, CartesianChart Chart) Chart(int count, SamplingMode sampling)
    {
        var readings = new List<double>(count);

        for (var index = 0; index < count; index++)
        {
            // A shape with peaks and troughs in it, so that thinning it is a decision with a visible answer.
            readings.Add(50 + (40 * Math.Sin(index / 50.0)));
        }

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

    /// <summary>How far the line's outline moved in the worst column, comparing two pictures of one window.</summary>
    /// <param name="first">One picture.</param>
    /// <param name="second">The other.</param>
    /// <returns>The largest movement, in pixels, of either the top or the bottom of the line.</returns>
    private static int WorstDrift(RenderTargetBitmap first, RenderTargetBitmap second)
    {
        var worst = 0;

        for (var column = 0; column < first.PixelSize.Width; column++)
        {
            var (topFirst, bottomFirst) = Ink.Column(first, column, Line);
            var (topSecond, bottomSecond) = Ink.Column(second, column, Line);

            if (topFirst < 0 || topSecond < 0)
            {
                // An empty column in one is only a difference if the other has ink in it, which a line that
                // crosses the whole plot never should: the drift is what is being measured, not the coverage.
                continue;
            }

            worst = Math.Max(worst, Math.Abs(topFirst - topSecond));
            worst = Math.Max(worst, Math.Abs(bottomFirst - bottomSecond));
        }

        return worst;
    }

    /// <summary>A chart whose drag is a test's to drive, the way the pointer handlers would drive it.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Wheel(double notches, Point at, KeyModifiers modifiers) => WheelZoom(notches, at, modifiers);

        public void Settle() => EndZoom();

        public void Down(Point at) => DragStart(at);

        public bool Move(Point at, KeyModifiers modifiers) => DragMove(at, modifiers);

        public void Up() => DragEnd();
    }
}
