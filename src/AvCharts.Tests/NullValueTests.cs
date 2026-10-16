using Avalonia;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Series;
using AvCharts.Transforms;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks what a series draws where a record has no value: a break by default, a join when asked, a zero where
/// the axis has one - and that none of the three changes the data or what the axes cover.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class NullValueTests
{
    [Fact]
    public void A_gap_is_the_default_and_breaks_the_run()
    {
        // Values 1 and 3 on an axis that runs from zero to ten, across a plot 400 pixels tall: 360 and 280.
        var transform = Transform();
        var series = Series(transform, [1.0, double.NaN, 3.0]);

        var runs = series.Runs(Context(transform));

        Assert.Equal(2, runs.Count);
        Assert.Equal([new Point(0, 360)], runs[0]);
        Assert.Equal([new Point(120, 280)], runs[1]);
    }

    [Fact]
    public void Connect_joins_the_line_across_a_gap()
    {
        var transform = Transform();
        var series = Series(transform, [1.0, double.NaN, 3.0], NullValueMode.Connect);

        var runs = series.Runs(Context(transform));

        // One run of two points: the record that had no reading is simply not there, and the line carries on.
        Assert.Single(runs);
        Assert.Equal([new Point(0, 360), new Point(120, 280)], runs[0]);
    }

    [Fact]
    public void Zero_draws_a_missing_value_where_the_axis_puts_zero()
    {
        var transform = Transform();
        var series = Series(transform, [1.0, double.NaN, 3.0], NullValueMode.Zero);

        var runs = series.Runs(Context(transform));

        // The middle record is drawn at zero, which on this axis is the foot of the plot.
        Assert.Single(runs);
        Assert.Equal([new Point(0, 360), new Point(60, 400), new Point(120, 280)], runs[0]);
    }

    [Fact]
    public void Zero_stays_missing_on_an_axis_with_no_zero()
    {
        // A logarithmic axis has no zero to put a missing value at, so the value stays missing - and a missing
        // value breaks the run, as it always does.
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
        transform.SetAxis(ChartDimension.X, new LinearAxis());
        transform.SetAxis(ChartDimension.Y, new LogAxis());

        var series = Series(transform, [1.0, double.NaN, 3.0], NullValueMode.Zero);

        Assert.Equal(2, series.Runs(Context(transform)).Count);
    }

    [Fact]
    public void A_missing_value_never_widens_the_axis_range()
    {
        // The axis covers what the data says, in every mode: a zero that a mode invents is drawn where the axis
        // puts zero, it does not make the axis describe a value that was never measured.
        foreach (var mode in new[] { NullValueMode.Gap, NullValueMode.Connect, NullValueMode.Zero })
        {
            var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
            transform.SetAxis(ChartDimension.X, new LinearAxis());
            transform.SetAxis(ChartDimension.Y, new LinearAxis());
            Series(transform, [10.0, double.NaN, 30.0], mode);

            var range = transform.GetLayout(ChartDimension.Y).UnitRange;

            Assert.Equal(10, range.Minimum);
            Assert.Equal(30, range.Maximum);
        }
    }

    [Fact]
    public void A_dimension_where_every_record_is_missing_covers_nothing()
    {
        // Every series empty on a dimension: the axis has nothing to cover, so its range is empty, and a range
        // with no width is mapped to the middle of the plot rather than divided into.
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
        transform.SetAxis(ChartDimension.X, new LinearAxis());
        transform.SetAxis(ChartDimension.Y, new LinearAxis());
        Series(transform, [double.NaN, double.NaN, double.NaN], NullValueMode.Gap);

        var range = transform.GetLayout(ChartDimension.Y).UnitRange;

        Assert.True(range.IsEmpty);
        Assert.Equal(0.5, range.Normalize(12));

        // And the chart it is on still works out a plot, rather than folding into nothing.
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis(),
        };
        chart.Series.Add(new ValueProbe { ItemsSource = [double.NaN, double.NaN], YSelector = value => value });
        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        Assert.True(chart.PlotBounds.Width > 0);
        Assert.True(chart.PlotBounds.Height > 0);
    }

    [Fact]
    public void Sampling_keeps_the_surviving_points_where_they_were()
    {
        // A thousand readings at the same height with one missing in the middle, thinned to the columns of the
        // plot: the missing record splits the run and does not move a single point that survived it.
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
        transform.SetAxis(ChartDimension.X, new LinearAxis { Minimum = 0, Maximum = 999 });
        transform.SetAxis(ChartDimension.Y, new LinearAxis { Minimum = 0, Maximum = 10 });

        var values = Enumerable.Repeat(10.0, 1_000).ToArray();
        values[500] = double.NaN;

        var series = Series(transform, values, NullValueMode.Gap);
        series.Sampling = SamplingMode.MinMax;

        var runs = series.Runs(Context(transform));

        Assert.Equal(2, runs.Count);
        Assert.True(runs[0].Count < 600, $"kept {runs[0].Count} points for six hundred columns");
        Assert.True(
            runs[0].Max(point => point.X) < runs[1].Min(point => point.X),
            "no point crossed the gap, so the missing record moved nothing");

        // Every point is at the same height: the value axis was not shifted by dropping the record either.
        Assert.All(runs.SelectMany(run => run), point => Assert.Equal(0, point.Y));
    }

    private static ValueProbe Series(DataTransform transform, double[] values, NullValueMode? mode = null)
    {
        var series = new ValueProbe { ItemsSource = values, YSelector = value => value };

        if (mode is { } nullValueMode)
        {
            series.NullValueMode = nullValueMode;
        }

        transform.AddElement(series);
        transform.Update();
        return series;
    }

    private static SeriesRenderContext Context(DataTransform transform) =>
        new(transform, transform.PlotBounds, 1);

    /// <summary>Zero to ten on both dimensions, across a plot six hundred by four hundred.</summary>
    private static DataTransform Transform()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 400) };
        transform.SetAxis(ChartDimension.X, new LinearAxis { Minimum = 0, Maximum = 10 });
        transform.SetAxis(ChartDimension.Y, new LinearAxis { Minimum = 0, Maximum = 10 });
        return transform;
    }
}
