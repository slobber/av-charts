using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using BenchmarkDotNet.Attributes;

namespace AvCharts.Benchmarks;

/// <summary>
/// The frames of a window being moved: the ones the kept thinning describes, the one a zoom makes it stop
/// describing, and the ones a feed makes stale.
/// </summary>
/// <remarks>
/// <para>
/// A drag changes neither the readings nor the zoom - it slides the window over both - so the thinning a series
/// kept for the last frame still describes this one, and drawing costs a map rather than a pass over every
/// reading. What the thinning depends on is the readings and the zoom, and each of those has a benchmark here:
/// a frame where the window changes size enough to want a different bucket, and a frame where the feed has
/// pushed something.
/// </para>
/// <para>
/// What it is worth: the pass over the readings that works the thinning out is what the expensive frames here
/// spend their time on, and the kept thinning is a quarter of a pixel column's worth of readings - about six
/// thousand of them for a plot this size, which is all a frame that can keep it has to map.
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class DragBenchmarks
{
    /// <summary>How many frames one measurement is made of, so that a frame is a frame of a drag.</summary>
    private const int Frames = 16;

    private BenchChart _chart = null!;

    [GlobalSetup]
    public void Setup() => Headless.Start();

    [IterationSetup]
    public void Fresh()
    {
        _chart = new BenchChart();
        _chart.Build();
    }

    /// <summary>
    /// A frame of a window being dragged: nothing about the readings or the zoom moves, so the thinning the last
    /// frame kept is the thinning this one draws from, and the frame costs a map and a paint.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int PanFrame()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            width = _chart.PanAndDraw();
        }

        return width;
    }

    /// <summary>
    /// A frame while the window is being resized about as much as it can be: a zoom of two either way is a
    /// bucket of a different width each frame, so the thinning kept for the last one no longer describes this
    /// one and is worked out from the readings again - the expensive frame of a wheel gesture.
    /// </summary>
    [Benchmark]
    public int ZoomFrameAcrossABucketChange()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            _chart.Surface.ZoomBy(frame % 2 == 0 ? 0.5 : 2.0, new Point(340, 160));
            width = _chart.Draw();
        }

        return width;
    }

    /// <summary>A frame of a window being dragged while the feed runs: the readings move, so what was kept is stale.</summary>
    [Benchmark]
    public int PanFrameWithTheFeedRunning()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            _chart.Append();
            width = _chart.PanAndDraw();
        }

        return width;
    }

    /// <summary>
    /// A frame with the feed running and nothing else moving, which is the same work: the readings arrived, so
    /// the thinning is stale either way.
    /// </summary>
    [Benchmark]
    public int FrameWithTheFeedRunningAndNothingMoving()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            _chart.Append();
            width = _chart.Draw();
        }

        return width;
    }

    /// <summary>A chart of a hundred thousand readings, with the room to draw it.</summary>
    private sealed class BenchChart
    {
        private readonly List<double> _values = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))];
        private readonly RenderTargetBitmap _picture = new(new PixelSize(680, 320));
        private readonly Window _window = new();

        public CartesianChart Surface { get; private set; } = new();

        /// <summary>Reads the readings and lays the chart out, which a frame does not pay for twice.</summary>
        public void Build()
        {
            Surface = new CartesianChart
            {
                HorizontalAxis = new LinearAxis(),
                VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
            };

            Surface.Series.Add(new LineSeries<double>
            {
                ItemsSource = _values,
                YSelector = value => value,
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 1.5,
                Sampling = SamplingMode.MinMax,
            });

            _window.Resources = Themes.ChartTheme.Create();
            _window.Content = Surface;
            Surface.Measure(new Size(680, 320));
            Surface.Arrange(new Rect(0, 0, 680, 320));
        }

        public int PanAndDraw()
        {
            Surface.PanBy(20, 0);
            return Draw();
        }

        /// <summary>Draws the chart as it stands, which is what a frame that moved the window some other way pays.</summary>
        public int Draw()
        {
            _picture.Render(Surface);
            return _picture.PixelSize.Width;
        }

        /// <summary>One reading arriving, applied the way the chart applies a batch: once, before the frame.</summary>
        public void Append()
        {
            var series = (CartesianSeries<double>)Surface.Series[0];
            series.Push(50 + (40 * Math.Sin(_feed / 500.0)));
            _feed++;
            _ = series.ApplyPushed();
        }

        private int _feed = 100_000;
    }
}
