using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// The coarse budgets continuous integration holds, so that a catastrophic regression is a failing build rather
/// than something noticed later.
/// </summary>
/// <remarks>
/// <para>
/// The real figures are measured by <c>AvCharts.Benchmarks</c> in Release, with BenchmarkDotNet taking the noise
/// out, and they are recorded in the design brief. What is here is deliberately blunt: a build agent shares its
/// processor with whatever else the agent is running, and a tight timing assertion on one is a flaky test rather
/// than a measurement - so these are a multiple of the budget, and the multiple is written down rather than
/// hidden in a threshold nobody can justify later.
/// </para>
/// <para>
/// Two of them are not about time at all: what a million readings cost to keep is a size, and a chart that is
/// twice as slow is a chart that is twice as slow, while a chart that keeps twice as much is a chart that stops
/// fitting in the machine it was meant for.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class PerformanceTests
{
    [Fact]
    public void A_hundred_thousand_readings_are_drawn_inside_the_disaster_budget()
    {
        // The brief's own coarse budget for CI: a tenth of a second of work should not become a second without
        // anyone noticing. Measured today at about 27ms (BenchmarkDotNet, Release).
        var chart = Chart(100_000, SamplingMode.MinMax);
        var bitmap = new RenderTargetBitmap(new PixelSize(680, 320));
        var started = Stopwatch.StartNew();

        bitmap.Render(chart);

        started.Stop();

        Assert.True(
            started.ElapsedMilliseconds < 1_000,
            $"drawing a hundred thousand readings took {started.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Finding_a_record_on_a_hundred_thousand_points_is_inside_its_budget()
    {
        // The brief gives this one five milliseconds, and it is measured at seventy microseconds: the index is
        // the readings themselves, so a query is a binary search rather than a walk. Twenty of them, taking the
        // middle rather than the mean, because a build agent occasionally stops for a cup of tea.
        var chart = Chart(100_000, SamplingMode.MinMax);
        var plot = chart.PlotBounds;
        var samples = new List<long>(21);

        for (var query = 0; query < 21; query++)
        {
            var at = new Point(plot.X + (plot.Width * query / 20.0), plot.Center.Y);
            var started = Stopwatch.StartNew();

            _ = chart.FindDataPoints(at);

            started.Stop();
            samples.Add(started.ElapsedTicks);
        }

        samples.Sort();
        var middle = samples[samples.Count / 2] * 1_000.0 / Stopwatch.Frequency;

        Assert.True(middle < 5, $"finding a record took {middle:0.###} ms");
    }

    [Fact]
    public void A_batch_of_ten_thousand_readings_is_applied_inside_a_frame()
    {
        // The brief's frame budget is eight milliseconds and this is measured at under one. The assertion is
        // three times the budget rather than the budget itself: this is the CI guard, and the budget is the
        // benchmark's job on the machine the chart is meant to run on.
        var chart = Chart(100_000, SamplingMode.None);
        var series = (LineSeries<double>)chart.Series[0];
        var started = Stopwatch.StartNew();

        for (var index = 0; index < 10_000; index++)
        {
            series.Push(50 + (index % 40));
        }

        var applied = series.ApplyPushed();
        started.Stop();

        Assert.Equal(10_000, applied);
        Assert.True(
            started.ElapsedMilliseconds < 24,
            $"applying a batch of ten thousand readings took {started.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void A_million_readings_cost_less_than_fifty_megabytes_to_keep()
    {
        // What a chart of a million readings costs to hold: two arrays of doubles for the values and one of
        // references back to the caller's records, which is twenty-four bytes a reading. Read once, kept, and
        // measured with the garbage collected - what is counted is what stays, not what was allocated getting
        // there.
        var readings = new double[1_000_000];

        for (var index = 0; index < readings.Length; index++)
        {
            readings[index] = 50 + (40 * Math.Sin(index / 5000.0));
        }

        var before = Retained();
        var series = new LineSeries<double> { ItemsSource = readings, YSelector = value => value };
        var chart = Chart(0, SamplingMode.None);

        chart.Series.Add(series);
        chart.Measure(new Size(680, 320));
        chart.Arrange(new Rect(0, 0, 680, 320));

        _ = series.Items.Count();

        var kept = Retained() - before;

        // The readings themselves are the caller's, and are held anyway: the budget is about what the library
        // keeps on top of them, so the caller's own eight megabytes are taken off.
        var library = kept - readings.Length * sizeof(double);
        var megabytes = library / (1024.0 * 1024.0);

        Assert.True(megabytes < 50, $"a million readings cost {megabytes:0.#} MB to keep");
    }

    /// <summary>A chart of so many readings, laid out and dressed.</summary>
    /// <param name="count">How many readings.</param>
    /// <param name="sampling">Whether the series samples.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(int count, SamplingMode sampling)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
        };

        if (count > 0)
        {
            // A list rather than an array: a reading can only be pushed into a source that will take one, and a
            // live feed is a list.
            var readings = new List<double>(count);

            for (var index = 0; index < count; index++)
            {
                readings.Add(50 + (40 * Math.Sin(index / 500.0)));
            }

            chart.Series.Add(new LineSeries<double>
            {
                ItemsSource = readings,
                YSelector = value => value,
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 1.5,
                Sampling = sampling,
            });
        }

        chart.Measure(new Size(680, 320));
        chart.Arrange(new Rect(0, 0, 680, 320));
        return chart;
    }

    /// <summary>How many bytes are being held, with the garbage collected first.</summary>
    /// <returns>The bytes.</returns>
    private static long Retained()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return GC.GetTotalMemory(forceFullCollection: true);
    }
}
