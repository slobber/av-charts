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
    public void A_million_points_cost_sixteen_bytes_each()
    {
        // The budget the design brief sets is about a million points at rest. The values are two arrays of
        // doubles, so the reading of a source costs sixteen bytes a point - plus what growing the arrays throws
        // away, which is at most as much again. The old pair-of-ChartValue store cost five times that.
        var source = new double[1_000_000];
        var series = new ValueProbe { ItemsSource = source, YSelector = value => value };
        var transform = Transform();
        transform.AddElement(series);

        var before = GC.GetAllocatedBytesForCurrentThread();

        transform.Update();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1_000_000, series.YExposed.Length);
        Assert.True(
            allocated < 40_000_000,
            $"reading a million points allocated {allocated} bytes, which is more than twice what sixteen bytes a point costs");
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
