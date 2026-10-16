using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the picture a chart can be asked for: the size it is drawn at, the size it comes back as, and that
/// asking for one leaves the chart on screen exactly as it was.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class ChartImageTests
{
    [Fact]
    public void The_picture_is_the_chart_at_the_size_it_has()
    {
        var chart = Laid(Chart());
        var picture = chart.RenderImage();

        Assert.Equal(new PixelSize(400, 200), picture.PixelSize);
        Assert.True(Ink.Count(picture) > 0, "the picture is blank");
        Assert.True(Ink.Count(picture, Colors.White) > 0, "the picture has no surface under the chart");
    }

    [Fact]
    public void A_picture_of_another_size_is_drawn_at_that_size_and_then_put_back()
    {
        // A chart is drawn by being laid out, so a picture of another size means laying it out at that size and then
        // laying it out again at the size it had - and the chart on screen has to come back exactly as it was.
        var chart = Laid(Chart());
        var was = chart.Bounds;
        var plot = chart.PlotBounds;

        var picture = chart.RenderImage(new PixelSize(600, 300));

        Assert.Equal(new PixelSize(600, 300), picture.PixelSize);
        Assert.True(Ink.Count(picture) > 0, "the picture is blank");
        Assert.Equal(was, chart.Bounds);
        Assert.Equal(plot, chart.PlotBounds);
    }

    [Fact]
    public void A_scaled_picture_is_the_same_chart_at_a_finer_pitch()
    {
        var chart = Laid(Chart());
        var plain = chart.RenderImage();
        var sharp = chart.RenderImage(scaling: 2);

        Assert.Equal(new PixelSize(800, 400), sharp.PixelSize);

        // The same drawing at twice the pitch: what is drawn where it is, and its ink reaching the same place in the
        // picture proportionally - which a stretch of the small picture would do too, and a different layout would not.
        var small = Ink.Bounds(plain);
        var large = Ink.Bounds(sharp);

        Assert.True(Ink.Count(sharp) > 0, "the picture is blank");
        Assert.InRange(large.Left, (2 * small.Left) - 4, (2 * small.Left) + 4);
        Assert.InRange(large.Top, (2 * small.Top) - 4, (2 * small.Top) + 4);
        Assert.InRange(large.Right, (2 * small.Right) - 6, (2 * small.Right) + 6);
        Assert.InRange(large.Bottom, (2 * small.Bottom) - 6, (2 * small.Bottom) + 6);
    }

    [Fact]
    public void Saving_a_chart_writes_a_png()
    {
        var chart = Laid(Chart());
        var bytes = new MemoryStream();

        chart.SaveImage(bytes);

        var written = bytes.ToArray();

        Assert.True(written.Length > 1000, $"the file is {written.Length} bytes");
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, written[..4]);
    }

    [Fact]
    public void Asking_for_a_picture_while_the_chart_is_being_laid_out_leaves_it_where_it_was()
    {
        // A picture at another size is written by laying the chart out at that size and then laying it out again
        // at the one it had - and the one place a host is likely to ask for one is from a size change, which is
        // inside a layout pass the chart itself is part of. So this is the arrangement the gallery's export page
        // is in: laying a chart out at another size while it is being laid out at this one, and coming back.
        var chart = Chart();

        _ = new Window { Resources = ChartTheme.Create(), Content = chart };

        RenderTargetBitmap? picture = null;
        var asked = false;

        chart.SizeChanged += (_, _) =>
        {
            if (asked)
            {
                return;
            }

            asked = true;
            picture = chart.RenderImage(new PixelSize(600, 300));
        };

        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));

        Assert.NotNull(picture);
        Assert.True(Ink.Count(picture!) > 0, "the picture is blank");
        Assert.Equal(new PixelSize(600, 300), picture!.PixelSize);

        // And the chart on screen is where it was, still laid out, and still drawn: what the second pass left
        // behind is the chart the host is looking at rather than the size the picture was asked for.
        Assert.Equal(new Rect(0, 0, 400, 200), chart.Bounds);
        Assert.True(chart.IsMeasureValid);
        Assert.True(chart.IsArrangeValid);

        var onScreen = new RenderTargetBitmap(new PixelSize(400, 200), new Vector(96, 96));

        onScreen.Render(chart);

        Assert.True(Ink.Count(onScreen) > 0, "the chart on screen is blank after the picture was written");
    }

    /// <summary>A chart with a line on it, laid out at the size the pictures are asked for.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
        };

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = [1.0, 5.0, 3.0, 8.0],
            YSelector = value => value,
            StrokeThickness = 2,
        });

        return chart;
    }

    /// <summary>Dresses a chart and lays it out, which is what it needs before it can be drawn at all.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The same chart.</returns>
    private static CartesianChart Laid(CartesianChart chart)
    {
        _ = new Window { Resources = ChartTheme.Create(), Content = chart };

        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));
        return chart;
    }
}
