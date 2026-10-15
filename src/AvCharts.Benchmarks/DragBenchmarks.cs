using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using BenchmarkDotNet.Attributes;

namespace AvCharts.Benchmarks;

/// <summary>
/// The frames of a drag: the one with nothing moving, the ones drawn from the thinning a series kept, the one
/// the thinning stops describing part-way through a zoom, and the same two with the feed running.
/// </summary>
/// <remarks>
/// <para>
/// A drag changes neither the readings nor the zoom - it slides the window over both - so the thinning the
/// library works out at drawing time is still the thinning the last frame worked out. A series keeps it in unit
/// space while a drag is on (<see cref="SeriesLayer.IsDragging"/>) and draws from it; letting go throws it away
/// and the next frame works it out from the readings again.
/// </para>
/// <para>
/// What it is worth: the pass over the readings that works the thinning out is what the frame with nothing
/// moving spends its time on, and the kept thinning is a quarter of a pixel column's worth of readings, which
/// is about six thousand of them for a plot this size and all a frame of a drag has to map.
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

    /// <summary>A frame with nothing moving: every reading is read, mapped and thinned again.</summary>
    [Benchmark(Baseline = true)]
    public int PanFrameExact()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            width = _chart.PanAndDraw();
        }

        return width;
    }

    /// <summary>A frame of a drag, drawn from the thinning the series kept.</summary>
    [Benchmark]
    public int PanFrameDragged()
    {
        _chart.Surface.SeriesLayer.IsDragging = true;

        try
        {
            var width = 0;

            for (var frame = 0; frame < Frames; frame++)
            {
                width = _chart.PanAndDraw();
            }

            return width;
        }
        finally
        {
            _chart.Surface.SeriesLayer.IsDragging = false;
        }
    }

    /// <summary>
    /// A frame of a drag while the window is being resized about as much as it can be: a zoom of two either way
    /// is a bucket of a different width each frame, so the thinning kept for the last one no longer describes
    /// this one and has to be worked out again - which is the expensive frame of a gesture, and the one the
    /// cost of a drag is measured against when the grip is a wheel rather than a pointer.
    /// </summary>
    [Benchmark]
    public int ZoomFrameDraggedAcrossABucketChange()
    {
        _chart.Surface.SeriesLayer.IsDragging = true;

        try
        {
            var width = 0;

            for (var frame = 0; frame < Frames; frame++)
            {
                _chart.Surface.ZoomBy(frame % 2 == 0 ? 0.5 : 2.0, new Point(340, 160));
                width = _chart.Draw();
            }

            return width;
        }
        finally
        {
            _chart.Surface.SeriesLayer.IsDragging = false;
        }
    }

    /// <summary>A frame of a drag while the feed is running: the readings are moving too, so what was kept is stale.</summary>
    [Benchmark]
    public int PanFrameDraggedWithTheFeedRunning()
    {
        _chart.Surface.SeriesLayer.IsDragging = true;

        try
        {
            var width = 0;

            for (var frame = 0; frame < Frames; frame++)
            {
                _chart.Append();
                width = _chart.PanAndDraw();
            }

            return width;
        }
        finally
        {
            _chart.Surface.SeriesLayer.IsDragging = false;
        }
    }

    /// <summary>
    /// And the same feed on the frames of a window that is not being dragged, which is the same work with the
    /// exact path instead of a thinning that the arriving readings keep making stale.
    /// </summary>
    [Benchmark]
    public int PanFrameExactWithTheFeedRunning()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            _chart.Append();
            width = _chart.PanAndDraw();
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
