using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using BenchmarkDotNet.Attributes;

namespace AvCharts.Benchmarks;

/// <summary>
/// The same frame, with how many readings there are and whether the series thins them out as parameters.
/// </summary>
/// <remarks>
/// <para>
/// A comparison is worth reading only if it says what each side was asked to do. The rival has no thinning of
/// its own, so "the same conditions" can only be either of two things - both sides drawing everything, or both
/// sides drawing a picture with about a thousand points in it - and this class is the AvCharts half of both.
/// </para>
/// <para>
/// Thinning is not free: it is a pass over every reading, and a frame of panning pays it again because the
/// window moved and the columns changed with it. That cost is part of what this measures, which is the point
/// of making it a parameter rather than a footnote.
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class SamplingBenchmarks
{
    /// <summary>How many frames one measurement is made of, so that a frame is not measured near the timer's noise floor.</summary>
    private const int Frames = 16;

    [Params(100_000, 1_300)]
    public int Readings { get; set; }

    [Params(SamplingMode.MinMax, SamplingMode.None)]
    public SamplingMode Sampling { get; set; }

    private BenchChart _chart = null!;

    [GlobalSetup]
    public void Setup() => Headless.Start();

    [IterationSetup]
    public void Fresh()
    {
        _chart = new BenchChart(Readings, Sampling);
        _chart.Build();
    }

    /// <summary>A frame of a drag: the window moved and the chart painted again.</summary>
    [Benchmark(OperationsPerInvoke = Frames)]
    public int PanFrame()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            _chart.Surface.PanBy(20, 0);
            width = _chart.Draw();
        }

        return width;
    }

    /// <summary>A chart of the readings asked for, thinned or not, with the room to draw it.</summary>
    private sealed class BenchChart
    {
        private readonly List<double> _values;
        private readonly RenderTargetBitmap _picture = new(new PixelSize(680, 320));
        private readonly Window _window = new();
        private readonly SamplingMode _sampling;

        public BenchChart(int readings, SamplingMode sampling)
        {
            _values = [.. Enumerable.Range(0, readings).Select(index => 50 + (40 * Math.Sin(index / 500.0)))];
            _sampling = sampling;
        }

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
                StrokeThickness = 1.5,
                Sampling = _sampling,
            });

            _window.Resources = Themes.ChartTheme.Create();
            _window.Content = Surface;
            Surface.Measure(new Size(680, 320));
            Surface.Arrange(new Rect(0, 0, 680, 320));
        }

        /// <summary>Paints the chart, which is what a frame costs.</summary>
        public int Draw()
        {
            _picture.Render(Surface);
            return _picture.PixelSize.Width;
        }
    }
}
