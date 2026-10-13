using Avalonia;
using Avalonia.Threading;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the thread model: a feed on another thread hands records to a series, and the series applies them in
/// one batch on the thread it is drawn on.
/// </summary>
/// <remarks>
/// The evidence that a batch is a batch is the number of times the series says it changed. A push that repainted
/// as it arrived would raise one per record; the arrangement the design brief asks for raises one for the frame.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ThreadingTests
{
    [Fact]
    public void A_push_waits_until_the_series_is_asked_to_apply_it()
    {
        var series = Feed();
        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        series.Push(9);

        Assert.Equal(1, series.PendingPushes);
        Assert.Empty(series.YExposed);

        Assert.Equal(1, series.ApplyPushed());
        Assert.Equal(0, series.PendingPushes);
        Assert.Equal([9d], series.YExposed);

        // Nothing left to do the second time.
        Assert.Equal(0, series.ApplyPushed());
    }

    [Fact]
    public void A_batch_costs_one_invalidation_for_the_whole_batch()
    {
        var series = Feed();
        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        var invalidations = 0;
        series.Invalidated += (_, _) => invalidations++;

        for (var index = 0; index < 100; index++)
        {
            series.Push(index);
        }

        Assert.Equal(0, invalidations);

        series.ApplyPushed();

        // A hundred points, one repaint: that is what keeps a feed of ten thousand a second off the render loop.
        Assert.Equal(1, invalidations);
        Assert.Equal(100, series.YExposed.Length);
    }

    [Fact]
    public void A_pushed_record_ends_up_in_the_source_too()
    {
        var source = new List<double>();
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };
        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        series.Push(1);
        series.Push(2);
        series.ApplyPushed();

        // The source holds everything the series shows, so a later read of it agrees with the values - which is
        // what makes the two in step rather than two histories.
        Assert.Equal([1d, 2d], source);
        Assert.Equal([1d, 2d], series.YExposed);

        series.InvalidateData();

        Assert.Equal([1d, 2d], series.YExposed);
    }

    [Fact]
    public void Pushing_needs_a_source_that_can_be_added_to()
    {
        var series = new ValueProbe { ItemsSource = new double[3], YSelector = value => value };

        // Said at the point of the mistake, on the thread that made it, rather than losing the record later.
        var error = Assert.Throws<InvalidOperationException>(() => series.Push(1));
        Assert.Contains("ItemsSource", error.Message);
    }

    [Fact]
    public void Pushing_from_many_threads_loses_nothing()
    {
        var series = Feed();
        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        var threads = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            for (var index = 0; index < 250; index++)
            {
                series.Push(index);
            }
        })).ToArray();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        Assert.Equal(1_000, series.PendingPushes);
        Assert.Equal(1_000, series.ApplyPushed());
        Assert.Equal(1_000, series.YExposed.Length);
    }

    [Fact]
    public void A_batch_pushed_into_a_stale_series_is_not_counted_twice()
    {
        var source = new List<double> { 1, 2, 3 };
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };
        var transform = Transform();
        transform.AddElement(series);

        // Nothing has been read yet, so the values are stale and the read that is coming will find the pushed
        // records in the source. Appending them as well would count them twice and draw each of them twice.
        series.Push(4);
        series.ApplyPushed();

        Assert.Equal([1d, 2d, 3d, 4d], series.YExposed);
    }

    [Fact]
    public void The_chart_applies_what_was_pushed_from_another_thread()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis(),
        };

        var series = Feed();
        chart.Series.Add(series);
        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        // A device thread handing over samples. It touches nothing but Push.
        var feed = new Thread(() =>
        {
            for (var index = 0; index < 5; index++)
            {
                series.Push(index);
            }
        });

        feed.Start();
        feed.Join();

        Assert.Equal(5, series.PendingPushes);
        Assert.Empty(series.YExposed);

        // The chart heard about the push on the feed's thread and asked for a pass on this one, which is the
        // thread it is drawn on.
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, series.PendingPushes);
        Assert.Equal([0d, 1d, 2d, 3d, 4d], series.YExposed);
    }

    private static ValueProbe Feed() =>
        new() { ItemsSource = new List<double>(), YSelector = value => value };

    private static DataTransform Transform()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
        transform.SetAxis(ChartDimension.X, new LinearAxis());
        transform.SetAxis(ChartDimension.Y, new LinearAxis());
        return transform;
    }
}
