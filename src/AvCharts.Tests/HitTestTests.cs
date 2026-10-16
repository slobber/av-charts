using Avalonia;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Series;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks what a pointer finds on a chart: the nearest record of each series within reach, the caller's own
/// object behind it, the records in a region, and the view a caller walks instead of a copy of the data.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class HitTestTests
{
    [Fact]
    public void The_nearest_record_of_a_series_is_the_one_a_pointer_finds()
    {
        var chart = Chart(Line([1.0, 2.0, 3.0]));
        var series = (LineSeries<double>)chart.Series[0];

        // A point a couple of pixels from the record for (2, 2).
        var near = Pixel(chart, 2, 2) + new Point(2, 2);

        var hit = series.HitTestItem(near, 5);

        Assert.NotNull(hit);
        Assert.Equal(1, hit.Value.ChartItem.Index);
        Assert.Equal(2, hit.Value.X.Number);
        Assert.Equal(2, hit.Value.Y.Number);
        Assert.True(hit.Value.Distance <= 5, $"found it {hit.Value.Distance} pixels away");
    }

    [Fact]
    public void The_line_between_two_records_is_ink_a_pointer_can_find()
    {
        // A reader points at the line they can see, and between two readings the line is what is there: the middle of
        // a climb is half a step from either end of it, and it used to be nowhere near a reading and so answered
        // nothing at all.
        var chart = Chart(Line([1.0, 2.0, 3.0]));
        var series = (LineSeries<double>)chart.Series[0];

        var hit = series.HitTestItem(Pixel(chart, 1.5, 1.5), 5);

        Assert.NotNull(hit);
        Assert.True(hit.Value.Distance <= 1, $"the line is {hit.Value.Distance} px from the pointer");
        Assert.InRange(hit.Value.ChartItem.Index, 0, 1);
    }

    [Fact]
    public void The_corner_of_a_step_is_ink_a_pointer_can_find()
    {
        // The shape the lines page draws and the shape a reader points at: the value held across from one reading to
        // the next, and the step at the far end of it. The corner is where neither of the two readings is.
        var chart = Chart(new StepLineSeries<double>
        {
            ItemsSource = [1.0, 3.0, 2.0],
            YSelector = value => value,
        });
        var series = (StepLineSeries<double>)chart.Series[0];

        var hit = series.HitTestItem(Pixel(chart, 1, 1), 5);

        Assert.NotNull(hit);
        Assert.Equal(0, hit.Value.ChartItem.Index);
        Assert.True(hit.Value.Distance <= 1, $"the corner is {hit.Value.Distance} px from the pointer");
    }

    [Fact]
    public void The_shaded_part_of_an_area_is_ink_a_pointer_can_find()
    {
        // A region is ink as much as its edge is, and a reader whose hand is inside the shading is pointing at it.
        var chart = Chart(new AreaSeries<double>
        {
            ItemsSource = [1.0, 3.0, 2.0],
            YSelector = value => value,
        });
        var series = (AreaSeries<double>)chart.Series[0];

        var inside = series.HitTestItem(Pixel(chart, 1, 1), 5);
        var above = series.HitTestItem(Pixel(chart, 1, 4), 5);

        Assert.NotNull(inside);
        Assert.True(inside.Value.Distance <= 1, $"inside the area is {inside.Value.Distance} px from the pointer");
        Assert.Null(above);
    }

    [Fact]
    public void A_reading_beats_the_ink_that_passes_through_it()
    {
        // Two readings of one series on the line y = x, so the ink between the first pair runs straight through the
        // reading that comes fourth. Pointing at a reading is pointing at that reading, whatever else is drawn
        // through the same pixel - which is what a tip stands for, so a series that answered otherwise would take
        // its own tip away.
        var chart = Chart(Line([1.0, 2.0, 3.0, 4.0, 1.5, 2.5]));
        var series = (LineSeries<double>)chart.Series[0];
        var readings = series.Items.ToArray();

        foreach (var index in new[] { 4, 5 })
        {
            var at = readings[index].Position!.Value;
            var hit = series.HitTestItem(at, 0.5);

            Assert.NotNull(hit);
            Assert.Equal(index, hit.Value.ChartItem.Index);
        }
    }

    [Fact]
    public void Nothing_is_found_beyond_the_sensitivity()
    {
        var chart = Chart(Line([1.0, 2.0, 3.0]));

        var far = Pixel(chart, 2, 2) + new Point(40, 40);

        Assert.Null(chart.Series[0].HitTestItem(far, 5));
    }

    [Fact]
    public void A_missing_value_is_never_a_hit()
    {
        // There is no ink where a record has no value, so there is nothing there to point at - the record keeps
        // its place in the data, not in the picture.
        var chart = Chart(Line([1.0, double.NaN, 3.0]));

        var whereItWouldBe = Pixel(chart, 1, 2);

        Assert.Null(chart.Series[0].HitTestItem(whereItWouldBe, 5));
        Assert.Empty(chart.FindDataPoints(whereItWouldBe));
    }

    [Fact]
    public void The_chart_finds_one_point_per_series_nearest_first()
    {
        var nearby = Line([1.0, 2.0, 3.0]);
        var further = Line([1.0, 2.0, 3.0]);
        var chart = Chart(nearby, further);

        var point = Pixel(chart, 2, 2) + new Point(1, 1);

        var hits = chart.FindDataPoints(point);

        Assert.Equal(2, hits.Count);
        Assert.Equal(hits.OrderBy(hit => hit.Distance), hits);
        Assert.All(hits, hit => Assert.Equal(1, hit.ChartItem.Index));
        Assert.All(hits, hit => Assert.NotNull(hit.DisplayText));
    }

    [Fact]
    public void A_hit_hands_back_the_callers_own_object()
    {
        // Whatever the pipeline does to the data, the object a caller handed over comes back - and with a filter
        // in play it is the object behind that record rather than whatever happens to sit at that position.
        var records = new List<Reading>
        {
            new("first", 1),
            new("dropped", 2),
            new("last", 3),
        };

        var series = new LineSeries<Reading>
        {
            ItemsSource = records,
            XSelector = item => item.Value,
            YSelector = item => item.Value,
            Filter = item => item.Name != "dropped",
        };

        var chart = Chart(series);
        var hit = chart.FindDataPoints(Pixel(chart, 3, 3)).Single();

        Assert.Same(records[2], hit.Item);
        Assert.Equal("last", ((Reading)hit.Item!).Name);
    }

    [Fact]
    public void The_view_is_the_records_in_view()
    {
        var series = Line([1.0, 2.0, 3.0, 4.0, 5.0]);
        var chart = Chart(series);

        // All five are in view on an axis that fits them.
        Assert.Equal(5, chart.GetAllDataPoints().Count());

        // Narrow the window to the middle of them and the view follows the window, not the data.
        chart.HorizontalAxis = new LinearAxis { Minimum = 2, Maximum = 4 };
        chart.Measure(new Avalonia.Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        var inView = chart.GetAllDataPoints().ToArray();

        Assert.Equal(3, inView.Length);
        Assert.Equal([1, 2, 3], inView.Select(item => item.Index));
        Assert.All(inView, item => Assert.Same(series, item.Series));
    }

    [Fact]
    public void A_record_with_no_vertical_value_is_still_in_the_view()
    {
        var series = new LineSeries<double>
        {
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => value,
            YSelector = value => value == 2 ? ChartValue.Empty : value,
        };

        var chart = Chart(series);

        var items = chart.GetAllDataPoints().ToArray();

        Assert.Equal(3, items.Length);
        Assert.True(items[1].Y.IsEmpty, "the record is there, with nothing in it");
        Assert.Equal(2, items[1].X.Number);
    }

    [Fact]
    public void GetItemsInRegion_finds_the_records_inside_it()
    {
        var series = Line([1.0, 2.0, 3.0, 4.0, 5.0]);
        var chart = Chart(series);

        // The plot narrows down to the record for three, so the records for one, two and three are inside and the
        // ones after them are not.
        var plot = chart.PlotBounds;
        var left = new Rect(plot.X, plot.Y, Pixel(chart, 3, 0).X - plot.X, plot.Height);

        var found = chart.GetItemsInRegion(left);

        Assert.Equal([0, 1, 2], found.Select(item => item.Index));
    }

    [Fact]
    public void A_tip_text_is_written_the_way_the_axes_write_their_labels()
    {
        var axis = new DateTimeAxis { Interval = 86_400_000 };
        var series = new LineSeries<double>
        {
            DisplayName = "Depth",
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero).AddDays(value),
            YSelector = value => value * 10,
        };

        var chart = new CartesianChart { HorizontalAxis = axis, VerticalAxis = new LinearAxis() };
        chart.Series.Add(series);
        chart.Measure(new Avalonia.Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        // Found where the second record is drawn.
        var where = series.Items.Single(item => item.Index == 1).Position!.Value;
        var hit = chart.FindDataPoints(where).Single();

        // The axes do the writing, so a tip reads exactly like the labels beside it.
        Assert.Equal("Depth: 2026-10-15, 20", hit.DisplayText);
    }

    [Fact]
    public void A_hundred_thousand_points_are_hit_tested_without_walking_them()
    {
        // A guard against a catastrophic regression rather than the budget itself: the design brief measures the
        // real figure with the benchmarks, and this only says that finding a point on a large series does not
        // cost the whole series every time a pointer moves.
        var values = Enumerable.Range(0, 100_000).Select(index => (double)(index % 100)).ToArray();
        var series = new LineSeries<double> { ItemsSource = values, YSelector = value => value, Sampling = SamplingMode.MinMax };
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
        };
        chart.Series.Add(series);
        chart.Measure(new Avalonia.Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        var started = System.Diagnostics.Stopwatch.StartNew();

        for (var probe = 0; probe < 200; probe++)
        {
            chart.FindDataPoints(new Point(chart.PlotBounds.X + (probe % 600), chart.PlotBounds.Center.Y));
        }

        started.Stop();

        Assert.True(
            started.ElapsedMilliseconds < 2_000,
            $"two hundred hit tests on a hundred thousand points took {started.ElapsedMilliseconds} ms");
    }

    /// <summary>The position a data value pair is drawn at, on a chart whose axes run from zero to ten.</summary>
    private static Point Pixel(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 10 * plot.Width), plot.Bottom - (y / 10 * plot.Height));
    }

    private static CartesianChart Chart(params SeriesBase[] series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        chart.Measure(new Avalonia.Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
        return chart;
    }

    private static LineSeries<double> Line(double[] values) =>
        new() { ItemsSource = values, XSelector = value => value, YSelector = value => value };

    /// <summary>Something with a name, so a hit can be traced back to the object it came from.</summary>
    private sealed record Reading(string Name, double Value);
}
