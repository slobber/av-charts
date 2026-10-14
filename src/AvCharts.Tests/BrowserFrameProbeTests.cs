using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Temporary: the desktop half of the browser measurement.
/// </summary>
/// <remarks>
/// <para>
/// The same chart the gallery's hundred-thousand page draws, the same gesture - a wheel zoom - and the same
/// marks, so that what a frame costs here can be put beside what it costs in a browser. The number of readings
/// the frame placed and the segments it spent its time in come out of <see cref="Probe"/>; what a whole frame
/// costs is timed here.
/// </para>
/// <para>
/// The chart is driven through the application's own loop rather than by hand: an invalidation that a manual
/// measure and arrange would leave sitting there is drained by the dispatcher and the render timer, which is the
/// only way a frame that is really drawn can be timed.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class BrowserFrameProbeTests
{
    /// <summary>How many frames each measurement runs for.</summary>
    private const int Frames = 12;

    [Fact]
    public void Report_what_a_zoom_frame_costs_on_the_desktop()
    {
        var readings = new List<double>(100_000);

        for (var index = 0; index < 100_000; index++)
        {
            readings.Add(50 + (40 * Math.Sin(index / 500.0)));
        }

        var chart = new ProbeChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
        };

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = readings,
            YSelector = value => value,
            Stroke = Brushes.OrangeRed,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        });

        var window = new Window
        {
            Width = 960,
            Height = 240,
            Resources = ChartTheme.Create(),
            Content = chart,
        };

        window.Show();
        Settle();

        var picture = new RenderTargetBitmap(new PixelSize(960, 240));
        var lines = new List<string>();

        // Four readings: the wheel's own frames and the exact ones, each through the compositor (which is what a
        // browser frame is) and through a bitmap (which is what the earlier desktop figures were taken with).
        var at = new Point(chart.PlotBounds.Center.X, chart.PlotBounds.Center.Y);

        lines.AddRange(Timed("wheel, compositor", chart, window, null, () => chart.Turn(1, at)));
        lines.AddRange(Timed("wheel, bitmap", chart, window, picture, () => chart.Turn(1, at)));
        lines.AddRange(Timed("exact, compositor", chart, window, null, () => chart.Zoom(1.2, at)));
        lines.AddRange(Timed("exact, bitmap", chart, window, picture, () => chart.Zoom(1.2, at)));

        window.Close();

        var report = string.Join(Environment.NewLine, lines);
        var folder = Path.Combine(Path.GetTempPath(), "avcharts-probe");

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "desktop.txt"), report);
        Console.WriteLine(report);
    }

    /// <summary>Zooms the chart over so many frames and says what each of them cost.</summary>
    /// <param name="what">What drives the frame, for the line.</param>
    /// <param name="chart">The chart.</param>
    /// <param name="window">The window it is in.</param>
    /// <param name="picture">A bitmap to render to as well, or null to let the compositor do the drawing.</param>
    /// <param name="step">What the frame does to the window before it is drawn.</param>
    /// <returns>The lines to report.</returns>
    private static List<string> Timed(
        string what,
        CartesianChart chart,
        Window window,
        RenderTargetBitmap? picture,
        Action step)
    {
        // A warm-up frame, so that nothing in the first measured one is a first-time cost: the envelope, the
        // geometry, the pens and the fonts have all been built by the time the clock starts.
        for (var frame = 0; frame < 2; frame++)
        {
            step();
            Draw(window, picture);
        }

        Probe.Reset();

        var started = Stopwatch.StartNew();

        for (var frame = 0; frame < Frames; frame++)
        {
            step();
            Draw(window, picture);
        }

        started.Stop();

        return
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"desktop {what}: {Frames} zoom frames in {started.Elapsed.TotalMilliseconds:0.0} ms, "
                + $"{started.Elapsed.TotalMilliseconds / Frames:0.00} ms a frame"),
            Probe.Report(),
        ];
    }

    /// <summary>Lets the application's own loop run the frame, and draws it if a bitmap was asked for.</summary>
    private static void Draw(Window window, RenderTargetBitmap? picture)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        if (picture is not null)
        {
            picture.Render(window);
        }
    }

    private static void Settle()
    {
        var until = Stopwatch.StartNew();

        while (until.ElapsedMilliseconds < 200)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        }
    }

    /// <summary>A chart whose gestures a test can drive, the way the pointer handlers would.</summary>
    private sealed class ProbeChart : CartesianChart
    {
        /// <summary>One notch of the wheel, which is the gesture the browser is slow at.</summary>
        /// <param name="notches">How many notches.</param>
        /// <param name="at">Where.</param>
        public void Turn(double notches, Point at) => WheelZoom(notches, at, KeyModifiers.None);

        /// <summary>The same zoom without the wheel, which is the exact path.</summary>
        /// <param name="factor">How much.</param>
        /// <param name="at">Where.</param>
        public void Zoom(double factor, Point at) => ZoomBy(factor, at);
    }
}
