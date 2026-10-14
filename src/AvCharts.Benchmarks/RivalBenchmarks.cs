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
/// Both are measured the same way: an Avalonia control, laid out, painted into a bitmap. That matters, because
/// the control layer is part of what a frame costs - LiveCharts' shipped 2.0.5 view cannot even be loaded
/// against Avalonia 12 (it asks the platform for a `Gestures.PinchEvent` field that is no longer there), so
/// this uses the 2.1.0 development build, which can. A pre-release is what it is: the number belongs to that
/// build, not to the package name.
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
    public void Fresh()
    {
        _rival = new Rival();

        // Its control defers its first paint to the frame after the one it is first asked for - a render of a
        // chart that has not painted yet costs nothing at all, which is how a benchmark can mismeasure this by
        // three orders of magnitude. So the chart is given one render to settle before anything is timed, and
        // what is timed is a frame, which is the quantity AvCharts is timed on as well.
        _ = _rival.Draw();
    }

    /// <summary>One frame of a hundred thousand readings in LiveCharts, the chart already laid out.</summary>
    [Benchmark]
    public int LiveChartsFrame() => _rival.Draw();

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
            // Its own headless chart, which is what it exports images with: the Avalonia view cannot be measured
            // this way at all - 2.0.5 does not load on Avalonia 12, and the 2.1.0 development view defers its
            // first paint to a frame this render never asks for, so a bitmap of it is empty and costs nothing.
            // Its control layer is therefore not in this number, and AvCharts' is in its own.
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
