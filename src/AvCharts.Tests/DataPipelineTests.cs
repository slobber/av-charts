using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the data pipeline: what the values cost, what makes them stale, and which changes to a source the
/// series follows rather than reads again.
/// </summary>
/// <remarks>
/// The distinguishing evidence is the number of times a selector runs. A series that follows a change reads the
/// records that changed; one that reads its source again reads all of them. Counting the calls says which
/// happened without the test having to know how the series stores anything.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class DataPipelineTests
{
    [Fact]
    public void A_record_appended_to_a_live_source_costs_only_that_record()
    {
        var source = new ObservableCollection<double> { 1, 2, 3 };
        var reads = 0;
        var series = new ValueProbe
        {
            ItemsSource = source,
            YSelector = value =>
            {
                reads++;
                return value;
            },
        };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        Assert.Equal(3, reads);

        source.Add(4);

        Assert.Equal(4, reads);
        Assert.Equal([1d, 2d, 3d, 4d], series.YExposed);
    }

    [Fact]
    public void A_record_replaced_where_it_stands_costs_only_that_record()
    {
        var source = new ObservableCollection<double> { 1, 2, 3 };
        var reads = 0;
        var series = new ValueProbe
        {
            ItemsSource = source,
            YSelector = value =>
            {
                reads++;
                return value;
            },
        };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();
        reads = 0;

        // What a live feed does to its newest point: update it in place.
        source[2] = 30;

        Assert.Equal(1, reads);
        Assert.Equal([1d, 2d, 30d], series.YExposed);
    }

    [Fact]
    public void A_record_removed_from_the_head_costs_only_the_records_that_left()
    {
        // What a rolling window does to its oldest readings - and the case a plain list cannot announce, which is
        // why the series has to be the one that trims. With the horizontal values coming from the records
        // themselves, nothing after the head moves: the records that left are the only ones that cost anything,
        // where reading the source again costs every record kept.
        var source = new ObservableCollection<double> { 1, 2, 3, 4 };
        var reads = 0;
        var series = new ValueProbe
        {
            ItemsSource = source,
            XSelector = value =>
            {
                reads++;
                return value;
            },
            YSelector = value => value,
        };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();
        reads = 0;

        source.RemoveAt(0);

        // Reading the values is what would read the source again, so ask for them before asking the count: a
        // series that marked itself stale would answer with the right readings and the whole source read.
        var values = series.YExposed;
        var horizontal = series.XExposed;

        Assert.Equal(0, reads);
        Assert.Equal([2d, 3d, 4d], horizontal);
        Assert.Equal([2d, 3d, 4d], values);
    }

    [Fact]
    public void A_bounded_series_keeps_the_newest_readings_and_lets_the_oldest_go()
    {
        // The window a live chart shows is the host's to decide; what the series decides is that what has left it
        // is not kept - the brief's "memory does not grow with time" - and what it drops it drops from the source
        // as well, so that reading the source again agrees with the values.
        var source = new List<double> { 1, 2, 3 };
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value, MaxReadings = 4 };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        for (var value = 4; value <= 9; value++)
        {
            series.Push(value);
        }

        series.ApplyPushed();

        Assert.Equal([6d, 7d, 8d, 9d], series.YExposed);
        Assert.Equal([6d, 7d, 8d, 9d], source);

        // And the horizontal values are the positions among the records kept, which have moved: they are read
        // again rather than left where the readings that left had them.
        Assert.Equal([0d, 1d, 2d, 3d], series.XExposed);
    }

    [Fact]
    public void A_bounded_series_over_an_announcing_source_drops_each_record_once()
    {
        // A source that announces its changes tells the series that the oldest record has left - but it is the
        // series that removed it, and the values that came with it have been dropped by the same call. Following
        // the announcement as well takes each record off the trace twice, which is a page that never settles: a
        // reading short of its window on every frame, and an empty trace a few seconds later.
        var source = new ObservableCollection<double> { 1, 2, 3, 4, 5 };
        var series = new ValueProbe
        {
            ItemsSource = source,
            XSelector = value => value,
            YSelector = value => value,
            MaxReadings = 4,
        };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        series.Push(6);
        series.ApplyPushed();

        Assert.Equal([3d, 4d, 5d, 6d], source);
        Assert.Equal([3d, 4d, 5d, 6d], series.YExposed);
    }

    [Fact]
    public void A_bounded_series_is_bounded_whatever_order_its_readings_are_in()
    {
        // The oldest reading is the one that arrived first, whatever the horizontal value it carries. Leaving a
        // series alone because its readings do not run in order is a bound that does nothing at all, quietly: the
        // source grows exactly as it would unbounded and the promise "memory does not grow with time" is kept by
        // nobody. A feed of values in no order at all is where the window is worth the most.
        var source = new List<double> { 5, 3, 9 };
        var series = new ValueProbe
        {
            ItemsSource = source,

            // Backwards on purpose: the readings run the other way along the axis.
            XSelector = value => -value,
            YSelector = value => value,
            MaxReadings = 3,
        };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        series.Push(1);
        series.ApplyPushed();

        Assert.Equal([3d, 9d, 1d], source);
        Assert.Equal([3d, 9d, 1d], series.YExposed);
    }

    [Fact]
    public void Bounding_needs_a_source_that_can_be_shortened()
    {
        var series = new ValueProbe { ItemsSource = new double[3], YSelector = value => value, MaxReadings = 2 };
        var transform = Transform();
        transform.AddElement(series);

        // Said where the mistake is, like pushing to a source that cannot be added to.
        var error = Assert.Throws<InvalidOperationException>(() => transform.Update());

        Assert.Contains("MaxReadings", error.Message);
    }

    [Fact]
    public void A_record_inserted_in_the_middle_reads_the_source_again()
    {
        var source = new ObservableCollection<double> { 1, 2, 3 };
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        source.Insert(0, 0);

        // Every record moved one place along, and the horizontal value of a record is its place among the kept
        // records, so the whole series is read again and the positions say so.
        Assert.Equal([0d, 1d, 2d, 3d], series.XExposed);
        Assert.Equal([0d, 1d, 2d, 3d], series.YExposed);
    }

    [Fact]
    public void A_record_removed_in_the_middle_reads_the_source_again()
    {
        var source = new ObservableCollection<double> { 1, 2, 3 };
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        source.RemoveAt(0);

        Assert.Equal([0d, 1d], series.XExposed);
        Assert.Equal([2d, 3d], series.YExposed);
    }

    [Fact]
    public void A_reset_reads_the_source_again()
    {
        var source = new ObservableCollection<double> { 1, 2, 3 };
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        source.Clear();
        source.Add(9);

        // Clearing announces a reset, which says nothing about what is left - how many records are gone, or
        // which - so the series reads its source again. The record added straight afterwards is read as part of
        // that one read rather than counted twice.
        Assert.Equal([9d], series.YExposed);
    }

    [Fact]
    public void An_item_that_announces_itself_is_followed()
    {
        var reading = new Reading(1);
        var source = new ObservableCollection<Reading> { reading };
        var series = new ValueProbe<Reading> { ItemsSource = source, YSelector = item => item.Value };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        Assert.Equal([1d], series.YExposed);

        // Nothing tells the series to read again: the item does, when it changes.
        reading.Value = 7;

        Assert.Equal([7d], series.YExposed);
    }

    [Fact]
    public void A_source_that_says_nothing_is_read_again_only_when_asked()
    {
        var source = new List<double> { 1, 2 };
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };

        var transform = Transform();
        transform.AddElement(series);
        transform.Update();

        source.Add(3);

        // A plain list does not announce anything, so the series has no way to know - which is what
        // InvalidateData is for, and what the documentation of ItemsSource says.
        Assert.Equal([1d, 2d], series.YExposed);

        series.InvalidateData();

        Assert.Equal([1d, 2d, 3d], series.YExposed);
    }

    [Fact]
    public void Changing_a_dimensions_axis_measures_the_values_again()
    {
        var transform = Transform();
        var series = new ValueProbe
        {
            ItemsSource = [1.0, 10.0, 100.0],
            XSelector = value => value,
            YSelector = value => 1.0,
        };
        transform.AddElement(series);
        transform.Update();

        Assert.Equal([1d, 10d, 100d], series.XExposed);

        // The same values on a logarithmic scale are exponents: the numbers did not change, the axis did, and
        // the kept unit values are only meaningful against the axis that measured them.
        transform.SetAxis(ChartDimension.X, new LogAxis());

        Assert.Equal([0d, 1d, 2d], series.XExposed);
    }

    [Fact]
    public void A_million_points_fit_the_memory_budget()
    {
        // The budget the design brief sets is about a million points at rest: two arrays of doubles for the
        // values, and the caller's own objects, which the pipeline keeps because a hit hands one back through
        // HitData.Item. That is twenty-four bytes a point, plus at most as much again for growing the arrays.
        // Measured at 50.3 MB for a million; the pair-of-ChartValue store this replaced was eighty bytes a point
        // before it kept the objects at all.
        var source = new double[1_000_000];
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };
        var transform = Transform();
        transform.AddElement(series);

        var before = GC.GetAllocatedBytesForCurrentThread();

        transform.Update();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1_000_000, series.YExposed.Length);

        // The budget is a Release measurement, and Release is where it is asserted: the same work allocates about
        // seventy-four megabytes in a Debug build against about fifty here - measured - and that difference is the
        // build's rather than the pipeline's. The brief's numbers, and the §11 budget they belong to, are Release.
#if !DEBUG
        Assert.True(
            allocated < 60_000_000,
            $"reading a million points allocated {allocated} bytes, and twenty-four bytes a point doubled by growth is fifty");
#endif
    }

    private static DataTransform Transform()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
        transform.SetAxis(ChartDimension.X, new LinearAxis());
        transform.SetAxis(ChartDimension.Y, new LinearAxis());
        return transform;
    }

    /// <summary>An item that tells whoever is listening when one of its values changes.</summary>
    private sealed class Reading(double value) : INotifyPropertyChanged
    {
        private double _value = value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

}
