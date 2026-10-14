using BenchmarkDotNet.Attributes;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace AvCharts.Benchmarks;

/// <summary>
/// The same work done by a rival: a chart of a hundred thousand readings painted once, and painted again after
/// its window has been moved.
/// </summary>
/// <remarks>
/// <para>
/// A comparison is worth reading only if it compares the same thing, and here that is "one chart of a hundred
/// thousand readings, drawn to a picture, on this machine". Both are given the same readings, both are the same
/// size, and both are measured by the same harness. What is <em>not</em> compared is everything else a charting
/// library is: its API, its features, its price, and how it behaves on a machine with a screen.
/// </para>
/// <para>
/// Two things about this are worth being straight about. LiveCharts is measured through its own headless chart -
/// the one it exports images with - because its Avalonia view cannot be loaded at all against Avalonia 12 (it
/// asks the platform for a `Gestures.PinchEvent` field that Avalonia 12 no longer has), so the Avalonia control
/// layer is not in its number while it is in AvCharts'. And AvCharts is measured by painting its control into a
/// bitmap, which is the whole of what a frame costs; the two numbers are the same kind of number, and that is
/// as far as the honesty of a microbenchmark goes.
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class RivalBenchmarks
{
    private Rival _rival = null!;

    [GlobalSetup]
    public void Setup() => Headless.Start();

    [IterationSetup]
    public void Fresh() => _rival = new Rival();

    /// <summary>A hundred thousand readings drawn for the first time, by LiveCharts.</summary>
    [Benchmark]
    public int LiveChartsFirstRender() => _rival.Draw();

    /// <summary>And a frame of panning it, which is the work AvCharts' own pan number is.</summary>
    [Benchmark]
    public int LiveChartsPanFrame()
    {
        _rival.Pan(20);
        return _rival.Draw();
    }

    /// <summary>A hundred thousand readings in LiveCharts, kept the way its documentation keeps one.</summary>
    private sealed class Rival
    {
        private readonly List<double> _values = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))];
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
                XAxes = [new Axis { MinLimit = 0, MaxLimit = 50_000 }],
                YAxes = [new Axis { MinLimit = 0, MaxLimit = 100 }],
            };
        }

        /// <summary>Moves the window along by a number of readings, the way a drag would.</summary>
        /// <param name="by">How far.</param>
        public void Pan(double by)
        {
            _from += by;
            _chart.XAxes.ElementAt(0).MinLimit = _from;
            _chart.XAxes.ElementAt(0).MaxLimit = _from + 50_000;
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
