using BenchmarkDotNet.Attributes;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace AvCharts.Benchmarks;

/// <summary>
/// The rival drawing about a thousand readings, which is the ink a hundred thousand of them thin down to.
/// </summary>
/// <remarks>
/// <para>
/// LiveCharts has no thinning of its own, so the only way to ask it for this picture is to hand it readings
/// that were thinned before they got there - which is what anyone using it for a hundred thousand readings has
/// to do. Everything else is as in <see cref="RivalBenchmarks"/>: its own headless chart, the same harness, the
/// same size, the same chart area.
/// </para>
/// <para>
/// The number belongs next to AvCharts' own: a chart that thins while drawing, and a chart whose readings were
/// thinned before it ever saw them, are the two ways of asking for the same picture, and the comparison is
/// only honest when both are on the table.
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class RivalSmallBenchmarks
{
    /// <summary>What a hundred thousand readings of this shape thin down to at six hundred pixels across.</summary>
    private const int Readings = 1_300;

    /// <summary>How many frames one measurement is made of, so that a frame is not measured near the timer's noise floor.</summary>
    private const int Frames = 16;

    private Rival _rival = null!;

    [GlobalSetup]
    public void Setup() => Headless.Start();

    [IterationSetup]
    public void Fresh()
    {
        _rival = new Rival();
        _ = _rival.Draw();
    }

    /// <summary>A frame of the same hundred thousand readings' worth of ink, painted by the rival.</summary>
    [Benchmark(OperationsPerInvoke = Frames)]
    public int LiveChartsSmallFrame()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            width = _rival.Draw();
        }

        return width;
    }

    /// <summary>And a frame of panning it, which is the work AvCharts' own pan number is.</summary>
    [Benchmark(OperationsPerInvoke = Frames)]
    public int LiveChartsSmallPanFrame()
    {
        var width = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            _rival.Pan(Readings * 0.0004);
            width = _rival.Draw();
        }

        return width;
    }

    /// <summary>A thousand three hundred readings in the rival, kept the way its documentation keeps one.</summary>
    private sealed class Rival
    {
        private readonly List<double> _values =
            [.. Enumerable.Range(0, Readings).Select(index => 50 + (40 * Math.Sin(index / 500.0)))];

        private readonly SKCartesianChart _chart;
        private double _from;

        public Rival()
        {
            _chart = new SKCartesianChart
            {
                Width = 680,
                Height = 320,
                Series =
                [
                    new LineSeries<double>
                    {
                        Values = _values,
                        Fill = null,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(new SKColor(0xE0, 0x7B, 0x39), 1.5f),
                    },
                ],

                // The window covers the readings, so that the picture is the same one AvCharts draws rather
                // than a squiggle in the left-hand corner of an empty chart.
                XAxes = [new Axis { MinLimit = 0, MaxLimit = Readings }],
                YAxes = [new Axis { MinLimit = 0, MaxLimit = 100 }],
            };
        }

        /// <summary>Moves the window along by a number of readings, the way a drag would.</summary>
        /// <param name="by">How far.</param>
        public void Pan(double by)
        {
            _from += by;
            _chart.XAxes.ElementAt(0).MinLimit = _from;
            _chart.XAxes.ElementAt(0).MaxLimit = _from + Readings;
        }

        /// <summary>Paints it, which is what a frame costs.</summary>
        /// <returns>The width of the picture, so that the work cannot be optimised away.</returns>
        public int Draw()
        {
            using var picture = _chart.GetImage();
            return picture.Width;
        }
    }
}
