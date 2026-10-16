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

        // And the picture is the size that was asked for, at twice the pixels to the inch: the same picture, more
        // of it in the same room. Which means a control that shows it has to scale it to its box - Avalonia's
        // Image does not, so the gallery's export page draws its own preview.
        Assert.Equal(new Size(400, 200), sharp.Size);
        Assert.Equal(new Vector(192, 192), sharp.Dpi);

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

        chart.SaveImage(bytes, scaling: 2);

        var written = bytes.ToArray();

        Assert.True(written.Length > 1000, $"the file is {written.Length} bytes");
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, written[..4]);

        // And the file is the picture's own pixels rather than the size it was asked for: what was saved at two
        // to a point is eight hundred across, which is what a reader who asks for twice as fine expects to get.
        Assert.Equal(800, (written[16] << 24) | (written[17] << 16) | (written[18] << 8) | written[19]);
        Assert.Equal(400, (written[20] << 24) | (written[21] << 16) | (written[22] << 8) | written[23]);
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

    [Fact]
    public async Task Saving_a_chart_asynchronously_writes_into_a_stream_that_only_writes_asynchronously()
    {
        // A file handle in a browser is a stream whose writes are asynchronous, and a picture handed to one with
        // the synchronous SaveImage comes out as a file of no bytes at all - which is what a reader of the
        // published gallery reported. So the stream here is one of those: it takes the synchronous write and
        // quietly does nothing with it, and only the asynchronous path lands.
        var chart = Laid(Chart());
        var file = new AsynchronousOnlyStream();

        var written = await chart.SaveImageAsync(file);

        Assert.True(written > 1000, $"the picture is {written} bytes");
        Assert.Equal(written, file.Written.Count);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, file.Written.Take(4).ToArray());
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

    /// <summary>A stream that only writes asynchronously, like a file handle in a browser.</summary>
    /// <remarks>
    /// The synchronous <see cref="Write(byte[], int, int)"/> is taken and dropped rather than refused: that is
    /// what was measured of the published gallery - the file was created, nothing raised, and it was empty.
    /// </remarks>
    private sealed class AsynchronousOnlyStream : Stream
    {
        /// <summary>What arrived through the asynchronous path.</summary>
        public List<byte> Written { get; } = [];

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => Written.Count;

        public override long Position
        {
            get => Written.Count;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Written.AddRange(buffer.Skip(offset).Take(count));
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Written.AddRange(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
