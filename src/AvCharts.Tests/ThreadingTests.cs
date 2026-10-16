using Avalonia;
using Avalonia.Threading;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Transforms;
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
    public void Pushing_asks_the_chart_to_lay_itself_out_again()
    {
        // A live chart's axis has to move with its readings, and where an axis puts its lines - and what its
        // labels say - is worked out in a layout pass rather than while painting. So a chart that applies a batch
        // of pushed records and only repaints keeps the axis it had: a reader watching one saw the labels sit
        // still at 00:00 to 00:05 for as long as they looked, and move only when something resized the chart.
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis(),
        };

        var series = Feed();

        chart.Series.Add(series);
        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        Assert.True(chart.IsArrangeValid, "the chart should be laid out to begin with");

        series.Push(9);

        Dispatcher.UIThread.RunJobs();

        Assert.False(chart.IsArrangeValid, "the chart should have asked to be laid out again");
    }

    [Fact]
    public void A_batch_that_stays_inside_what_the_axes_cover_does_not_ask_for_a_layout()
    {
        // The other half of the same question. A reading arriving where the axes already reach changes the ink
        // and nothing else: where their lines go, what their labels say and how much room they take are all read
        // off what they cover, and that has not moved. Laying the chart out again for one is a frame's work for
        // no change to the frame - measured on a live page, two to four layouts a frame, one for every batch of
        // a feed arriving every few milliseconds.
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, ScaleMode = AxisScaleMode.Fixed },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, ScaleMode = AxisScaleMode.Fixed },
        };

        var series = Feed();

        chart.Series.Add(series);
        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        Assert.True(chart.IsArrangeValid, "the chart should be laid out to begin with");

        series.Push(9);

        Dispatcher.UIThread.RunJobs();

        // The reading is applied - the picture has it - and the chart has not asked to work itself out again,
        // because there is nothing new for it to work out.
        Assert.Equal([9d], series.YExposed);
        Assert.True(chart.IsArrangeValid, "the chart asked to be laid out for a reading it already covered");
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
