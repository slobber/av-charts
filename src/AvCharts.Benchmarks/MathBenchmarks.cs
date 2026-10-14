using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using BenchmarkDotNet.Attributes;

namespace AvCharts.Benchmarks;

/// <summary>
/// The arithmetic behind a chart of a hundred thousand readings: reading the source, describing it to the axes,
/// sampling it, mapping it, and finding a record under a point.
/// </summary>
/// <remarks>
/// No Avalonia in most of these: the tables in the design brief are about the math layer, and a measurement of
/// the math layer should not be a measurement of a renderer. The two that do draw say so.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class MathBenchmarks
{
    private double[] _values = [];
    private LineSeries<double> _series = null!;
    private DataTransform _transform = null!;
    private IReadOnlyList<Point> _points = [];

    [GlobalSetup]
    public void Setup()
    {
        _values = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))];

        _series = new LineSeries<double>
        {
            ItemsSource = _values,
            YSelector = value => value,
            Stroke = Brushes.OrangeRed,
            Sampling = SamplingMode.MinMax,
        };

        _transform = new DataTransform
        {
            PlotBounds = new Rect(40, 10, 600, 300),
        };

        _transform.SetAxis(ChartDimension.X, new LinearAxis());
        _transform.SetAxis(ChartDimension.Y, new LinearAxis());
        _transform.AddElement(_series);
        _transform.Update();

        _points = [.. Enumerable.Range(0, 100_000).Select(index => new Point(index / 1000.0, 5 + (index % 90)))];
    }

    /// <summary>The first read of a hundred thousand records: selectors, values, and the two unit arrays.</summary>
    [Benchmark]
    public int ReadValues()
    {
        _series.InvalidateData();
        _transform.Update();
        return _series.Items.Count();
    }

    /// <summary>Describing the same readings to the axes, which is what sizes them.</summary>
    [Benchmark]
    public void DescribeData()
    {
        _series.InvalidateData();
        _transform.InvalidateRange();
        _transform.Update();
    }

    /// <summary>Mapping a hundred thousand points onto the plot, which is what a series does once per change.</summary>
    [Benchmark]
    public double Map()
    {
        var sum = 0d;

        foreach (var point in _points)
        {
            if (_transform.TryUnitToPoint(point.X, point.Y, out var mapped))
            {
                sum += mapped.X;
            }
        }

        return sum;
    }

    /// <summary>Thinning a hundred thousand points down to the columns there are, the cheap way.</summary>
    [Benchmark]
    public int SampleMinMax() => Sampler.Reduce(_points, 600, SamplingMode.MinMax).Count;

    /// <summary>And the expensive way, which picks points by the triangle they make.</summary>
    [Benchmark]
    public int SampleLttb() => Sampler.Reduce(_points, 600, SamplingMode.Lttb).Count;

    /// <summary>Working out where the ticks go and what they say, for a plot six hundred pixels wide.</summary>
    [Benchmark]
    public int ResolveAxis()
    {
        var axis = new LinearAxis();
        var layout = axis.Resolve([new BoundedValue(0), new BoundedValue(1000)], 600);
        return layout.MajorTicks.Count;
    }

    /// <summary>The reading the brief asks about by name: finding a record on a hundred thousand points.</summary>
    [Benchmark]
    public int FindDataPoints() => _transform.TryUnitToPoint(50, 50, out var at)
        ? _series.HitTestItem(at, 5) is { } hit ? hit.ChartItem.Index : -1
        : -1;
}

/// <summary>
/// A hundred thousand readings drawn once, and a frame of panning that chart.
/// </summary>
/// <remarks>
/// This one does draw: it uses the headless platform with Skia behind it, which is the same arrangement the
/// golden tests use - a frame time measured without a renderer is a measurement of nothing.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class RenderBenchmarks
{
    private BenchChart _chart = null!;

    [GlobalSetup]
    public void Setup() => Headless.Start();

    [IterationSetup]
    public void Fresh()
    {
        _chart = new BenchChart();
        _chart.Build();
    }

    /// <summary>A hundred thousand readings drawn for the first time, with sampling on.</summary>
    [Benchmark]
    public int FirstRender() => _chart.Draw();

    /// <summary>
    /// One frame of a drag: the window moved and the chart painted again, which is the whole of what a frame
    /// of panning costs - the readings are read, mapped, sampled, and drawn.
    /// </summary>
    [Benchmark]
    public int PanFrame()
    {
        _chart.Surface.PanBy(20, 0);
        return _chart.Draw();
    }

    /// <summary>A hundred thousand readings arriving in one batch, which is one frame of a live feed.</summary>
    [Benchmark]
    public int PushFrame()
    {
        var series = (CartesianSeries<double>)_chart.Surface.Series[0];

        for (var index = 0; index < 10_000; index++)
        {
            series.Push(50 + (index % 40));
        }

        return _chart.Surface.Series[0].ApplyPushed();
    }

    /// <summary>A chart dressed, drawn and then dressed again, which is what the variant switching costs.</summary>
    [Benchmark]
    public int SwitchVariant() => _chart.Switch();

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
                StrokeThickness = 1.5,
                Sampling = SamplingMode.MinMax,
            });

            // A window is what dresses a chart and what a variant is asked of, exactly as in a host: the headless
            // platform makes one without a screen.
            _window.Resources = Themes.ChartTheme.Create();
            _window.Content = Surface;
            Surface.Measure(new Size(680, 320));
            Surface.Arrange(new Rect(0, 0, 680, 320));
        }

        /// <summary>Paints the chart, which is what a frame costs.</summary>
        public int Draw() => Draw(_picture);

        /// <summary>Dresses the chart in the other variant, and paints it again.</summary>
        public int Switch()
        {
            // The variant is asked for at the top level, which is where a host asks for it: a chart reads the
            // variant it is actually shown in rather than carrying one of its own.
            _window.RequestedThemeVariant = _window.RequestedThemeVariant == ThemeVariant.Dark
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

            return Draw();
        }

        private int Draw(RenderTargetBitmap picture)
        {
            picture.Render(Surface);
            return picture.PixelSize.Width;
        }
    }
}
