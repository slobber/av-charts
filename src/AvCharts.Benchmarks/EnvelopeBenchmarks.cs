using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using BenchmarkDotNet.Attributes;

namespace AvCharts.Benchmarks;

/// <summary>
/// A prototype, measured: the thinning kept in data space and re-mapped on a drag, against the thinning redone
/// from the readings every frame.
/// </summary>
/// <remarks>
/// <para>
/// The library thins at drawing time, over the points it has just mapped, bucketed by pixel column. That is
/// the right place while the readings or the zoom are changing, because the picture has to be worked out
/// again anyway - but a drag changes neither. It only moves the window along, and a hundred thousand readings
/// are read, mapped and thinned again to move ink that was already known.
/// </para>
/// <para>
/// <see cref="EnvelopeLineSeries"/> keeps a min/max envelope of the readings in unit space - about two points
/// a pixel column at the zoom in force - and re-maps that on a drag. It is a prototype in what it does not
/// do: gaps inside one bucket break the run at the bucket's end rather than exactly where the reading is
/// missing, the bucket is keyed on the zoom and not on the visible range as well, and it is MinMax only. The
/// question it answers is the number, and the picture it draws is checked against the library's own thinning
/// rather than assumed to be the same (they are not pixel-for-pixel the same: see the design brief).
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class EnvelopeBenchmarks
{
    /// <summary>How many frames one measurement is made of, so that a frame is a frame of a drag.</summary>
    private const int Frames = 16;

    private BenchChart _thinned = null!;
    private BenchChart _envelope = null!;
    private BenchChart _finer = null!;

    [GlobalSetup]
    public void Setup() => Headless.Start();

    [IterationSetup]
    public void Fresh()
    {
        _thinned = new BenchChart(envelope: null);
        _envelope = new BenchChart(envelope: new EnvelopeLineSeries(thinOnDraw: false));
        _finer = new BenchChart(envelope: new EnvelopeLineSeries(thinOnDraw: true));

        _thinned.Build();
        _envelope.Build();
        _finer.Build();
    }

    /// <summary>What the library does today: read, map and thin every reading, on every frame of the drag.</summary>
    [Benchmark(Baseline = true)]
    public int PanFrameThinnedEveryFrame() => Drag(_thinned);

    /// <summary>The same frame with the thinning cached in data space.</summary>
    [Benchmark]
    public int PanFrameEnvelope() => Drag(_envelope);

    /// <summary>
    /// And with the envelope kept four times finer than the picture and the library's own sampler deciding the
    /// ink on top of it, which is as close as a cache can come to the picture the library draws today.
    /// </summary>
    [Benchmark]
    public int PanFrameEnvelopeFiner() => Drag(_finer);

    private static int Drag(BenchChart chart)
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            width = chart.PanAndDraw();
        }

        return width;
    }

    /// <summary>A chart of a hundred thousand readings, drawn by whichever series it is handed.</summary>
    private sealed class BenchChart
    {
        private readonly List<double> _values = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))];
        private readonly RenderTargetBitmap _picture = new(new PixelSize(680, 320));
        private readonly Window _window = new();
        private readonly LineSeries<double>? _envelope;

        public BenchChart(LineSeries<double>? envelope) => _envelope = envelope;

        public CartesianChart Surface { get; private set; } = new();

        public void Build()
        {
            Surface = new CartesianChart
            {
                HorizontalAxis = new LinearAxis(),
                VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
            };

            if (_envelope is null)
            {
                Surface.Series.Add(new LineSeries<double>
                {
                    ItemsSource = _values,
                    YSelector = value => value,
                    Stroke = Brushes.OrangeRed,
                    StrokeThickness = 1.5,
                    Sampling = SamplingMode.MinMax,
                });
            }
            else
            {
                _envelope.ItemsSource = _values;
                _envelope.YSelector = value => value;
                _envelope.Stroke = Brushes.OrangeRed;
                _envelope.StrokeThickness = 1.5;
                Surface.Series.Add(_envelope);
            }

            _window.Resources = Themes.ChartTheme.Create();
            _window.Content = Surface;
            Surface.Measure(new Size(680, 320));
            Surface.Arrange(new Rect(0, 0, 680, 320));
        }

        public int PanAndDraw()
        {
            Surface.PanBy(20, 0);
            _picture.Render(Surface);
            return _picture.PixelSize.Width;
        }
    }

    /// <summary>
    /// The prototype: a line whose thinned readings are kept in data space and re-mapped on a drag.
    /// </summary>
    private sealed class EnvelopeLineSeries : LineSeries<double>
    {
        private readonly bool _thinOnDraw;
        private (double X, double Y)[] _envelope = [];
        private int _envelopeCount;
        private int _bucket = -1;
        private int _data;
        private int _built = -1;
        private Geometry? _ink;

        /// <summary>Creates the series, coarsely or with the library's sampler still deciding the ink.</summary>
        /// <param name="thinOnDraw">
        /// When true the envelope is kept four times finer than the picture and the library's sampler thins it
        /// while drawing, which is the same decision the library makes today, taken over the envelope instead of
        /// over the readings.
        /// </param>
        public EnvelopeLineSeries(bool thinOnDraw) => _thinOnDraw = thinOnDraw;

        /// <inheritdoc />
        protected override void UpdateData()
        {
            base.UpdateData();
            _data++;
        }

        /// <inheritdoc />
        public override int ApplyPushed()
        {
            var applied = base.ApplyPushed();
            _data++;
            return applied;
        }

        /// <inheritdoc />
        protected override void BuildInk(in SeriesRenderContext context)
        {
            var x = XValues;
            var y = YValues;
            var columns = Math.Max(2, (int)Math.Round(context.PlotBounds.Width));
            var bucket = Bucket(x, _thinOnDraw ? columns * 4 : columns);

            if (bucket != _bucket || _data != _built)
            {
                Rebuild(x, y, bucket);
                _bucket = bucket;
                _built = _data;
            }

            var batch = new GeometryBatch();
            var run = new List<Point>();

            for (var index = 0; index < _envelopeCount; index++)
            {
                var (unitX, unitY) = _envelope[index];

                if (double.IsNaN(unitX) || double.IsNaN(unitY) || !TryPoint(unitX, unitY, out var point))
                {
                    Flush(batch, run, columns);
                    run.Clear();
                    continue;
                }

                run.Add(point);
            }

            Flush(batch, run, columns);
            _ink = batch.Build();
        }

        /// <inheritdoc />
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            base.Draw(context, in renderContext);

            var stroke = Stroke ?? PaletteStroke?.Brush;

            if (_ink is not null && stroke is not null)
            {
                context.DrawGeometry(null, new Pen(stroke, StrokeThickness), _ink);
            }
        }

        /// <summary>Adds a run, thinned by the library's own sampler when that is what was asked for.</summary>
        private void Flush(GeometryBatch batch, List<Point> run, int columns)
        {
            if (run.Count < 2)
            {
                return;
            }

            batch.AddPolyline(_thinOnDraw ? Sampler.Reduce(run, columns, SamplingMode.MinMax) : run);
        }

        /// <summary>How many readings one bucket holds: enough of them that a bucket is about a pixel column.</summary>
        private int Bucket(ReadOnlySpan<double> x, int columns)
        {
            var visible = x.Length;

            if (x.Length > 1 &&
                Transform is { } transform &&
                AxisFor(ChartDimension.X) is { } axis)
            {
                var range = transform.GetLayout(axis).UnitRange;
                var span = x[^1] - x[0];

                if (!range.IsEmpty && span > 0)
                {
                    var share = Math.Clamp((range.Maximum - range.Minimum) / span, 0, 1);
                    visible = Math.Max(1, (int)Math.Round(x.Length * share));
                }
            }

            return Math.Max(1, visible / columns);
        }

        /// <summary>The lowest and the highest reading in every bucket, in unit space, in x order.</summary>
        private void Rebuild(ReadOnlySpan<double> x, ReadOnlySpan<double> y, int bucket)
        {
            var buckets = ((x.Length + bucket) - 1) / bucket;
            var envelope = new (double X, double Y)[buckets * 2];
            var count = 0;

            for (var start = 0; start < x.Length; start += bucket)
            {
                var end = Math.Min(x.Length, start + bucket);
                var lowX = 0d;
                var lowY = 0d;
                var highX = 0d;
                var highY = 0d;
                var hasLow = false;
                var hasHigh = false;
                var hasGap = false;

                for (var index = start; index < end; index++)
                {
                    var unitX = x[index];
                    var unitY = y[index];

                    if (double.IsNaN(unitX) || double.IsNaN(unitY))
                    {
                        hasGap = true;
                        continue;
                    }

                    if (!hasLow || unitY < lowY)
                    {
                        lowX = unitX;
                        lowY = unitY;
                        hasLow = true;
                    }

                    if (!hasHigh || unitY > highY)
                    {
                        highX = unitX;
                        highY = unitY;
                        hasHigh = true;
                    }
                }

                if (!hasLow)
                {
                    envelope[count++] = (double.NaN, double.NaN);
                    continue;
                }

                // In x order, so that the line stays a run: the readings arrive in order, and a bucket's two
                // extremes come out in whichever order they were found.
                if (hasHigh && highX < lowX)
                {
                    envelope[count++] = (highX, highY);
                    envelope[count++] = (lowX, lowY);
                }
                else
                {
                    envelope[count++] = (lowX, lowY);

                    if (hasHigh && !highY.Equals(lowY))
                    {
                        envelope[count++] = (highX, highY);
                    }
                }

                // A bucket with a reading missing in it ends the run there: a gap is not to be drawn over.
                if (hasGap)
                {
                    envelope[count++] = (double.NaN, double.NaN);
                }
            }

            _envelope = envelope;
            _envelopeCount = count;
        }
    }
}
