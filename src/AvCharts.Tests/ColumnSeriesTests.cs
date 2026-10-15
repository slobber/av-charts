using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the series that stand in bands: how wide a column is and where it stands, what the chart's layout
/// mode makes of several of them, and what stacking adds up to.
/// </summary>
/// <remarks>
/// The geometry of a column is a rectangle, so the evidence is the ink it leaves: a series is painted on its own
/// into a bitmap and the bounds of what it drew are measured. That is the same trick the first golden image used
/// to prove the visual tree reached the layers.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ColumnSeriesTests
{
    [Fact]
    public void A_column_fills_a_fraction_of_its_slot_and_stands_in_the_middle_of_it()
    {
        var series = Column([5.0]);
        var chart = Chart(series);
        var plot = chart.PlotBounds;
        var ink = InkOf(chart, series);

        // One slot, so the band is the whole plot: a column is 0.65 of it wide, and its ink is inside 0.5 of it.
        Close(plot.Width * ColumnSeries<double>.DefaultColumnWidthRatio, ink.Right - ink.Left);
        Close(plot.X + (plot.Width / 2), (ink.Left + ink.Right) / 2.0);

        // And it stands on zero, reaching its own value: five of a vertical axis that runs to ten.
        Close(plot.Bottom, ink.Bottom);
        Close(plot.Bottom - (plot.Height / 2), ink.Top);
    }

    [Fact]
    public void The_cap_decides_the_width_when_it_is_smaller_than_the_fraction()
    {
        var series = Column([5.0]);
        series.MaxColumnWidth = 12;

        var ink = InkOf(Chart(series), series);

        Close(12, ink.Right - ink.Left);
    }

    [Fact]
    public void A_width_ratio_of_nothing_is_a_mistake_rather_than_a_setting()
    {
        var series = new ColumnSeries<double>();

        Assert.Throws<ArgumentOutOfRangeException>(() => series.ColumnWidthRatio = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => series.MaxColumnWidth = -1);

        // A cap of null is how a caller says "no cap", so it is not a mistake.
        series.MaxColumnWidth = null;
    }

    [Fact]
    public void A_column_with_no_value_keeps_its_slot_and_draws_nothing()
    {
        var series = new ColumnSeries<Reading>
        {
            ItemsSource = [new Reading("a", 5), new Reading("b", double.NaN), new Reading("c", 5)],
            XSelector = reading => reading.Category,
            YSelector = reading => reading.Value,
        };

        var chart = Chart(series);
        var layout = chart.Transform.GetLayout(ChartDimension.X);

        // Half a slot of room either side of the three categories the axis learned, so the first and last
        // columns are whole rather than cut in half.
        Assert.Equal(-0.5, layout.UnitRange.Minimum);
        Assert.Equal(2.5, layout.UnitRange.Maximum);

        // Two columns are drawn and the middle slot is empty: the ink is two bars' worth, not three.
        var ink = InkOf(chart, series);
        var bitmap = InkBitmap(chart, series);
        var bar = (ink.Right - ink.Left) / 2.0;
        var height = ink.Bottom - ink.Top;

        Assert.Equal(2 * bar * height, Ink.Count(bitmap), 2 * (bar + height));
    }

    [Fact]
    public void A_bar_is_a_column_lying_on_its_side()
    {
        var series = new BarSeries<Reading>
        {
            ItemsSource = [new Reading("a", 1)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Category,
            Fill = Brushes.Red,
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new CategoryAxis(),
        };
        chart.Series.Add(series);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        var plot = chart.PlotBounds;
        var ink = InkOf(chart, series);

        // Its height is the fraction of a slot and its length is the value: a short value gives a bar wider than
        // it is tall, which is what says the bands run up the chart.
        Close(plot.Height * ColumnSeries<double>.DefaultColumnWidthRatio, ink.Bottom - ink.Top);
        Close(plot.Width / 10, ink.Right - ink.Left);
    }

    [Fact]
    public void A_pointer_on_a_column_is_on_the_column()
    {
        // The bug this is here for: a column was answered as if it were a mark at its reading's place on the axis,
        // which is where a column is not. Side by side, the readings of both series stand in the middle of the
        // slot, so a pointer on either column was within reach of neither - and the tip that did appear appeared
        // wherever a value happened to be, not where a column was drawn.
        var first = Column([5.0]);
        var second = Column([5.0]);
        var chart = Chart(SeriesLayout.Cluster, first, second);
        var plot = chart.PlotBounds;

        // What was drawn, and then pointed at: a pointer arrives between frames, and a series is told how its
        // chart lays the bands out as it draws.
        var left = MiddleOf(chart, first);
        var right = MiddleOf(chart, second);

        Assert.Equal(first, Assert.Single(chart.FindDataPoints(left)).ChartItem.Series);
        Assert.Equal(second, Assert.Single(chart.FindDataPoints(right)).ChartItem.Series);

        // And where there is no ink there is nothing to point at: above the columns, and to the side of them.
        Assert.Empty(chart.FindDataPoints(new Point(left.X, plot.Top + 2)));
        Assert.Empty(chart.FindDataPoints(new Point(plot.X + 2, plot.Bottom - 2)));
    }

    [Fact]
    public void A_stacked_column_is_answered_where_it_is_drawn()
    {
        var bottom = Column([3.0]);
        var top = Column([4.0]);
        var chart = Chart(SeriesLayout.Stacked, bottom, top);
        var plot = chart.PlotBounds;
        var drawn = MiddleOf(chart, top);

        // Pointing at the upper column names the upper series, which is the whole of what stacking broke: the
        // reading of the series above is drawn higher up than its value, and its value's place is inside the
        // column below it - so the answer used to be the series underneath, or nothing at all.
        Assert.Equal(top, Assert.Single(chart.FindDataPoints(drawn)).ChartItem.Series);

        var under = MiddleOf(chart, bottom);

        Assert.Equal(bottom, Assert.Single(chart.FindDataPoints(under)).ChartItem.Series);

        // And the record's own place is where its column is, not where its value would be as a mark: the anchor a
        // tip hangs from is the top of the stack.
        var item = top.Items.Single(record => record.Index == 0);
        var ink = InkOf(chart, top);

        Close(ink.Top, item.Position!.Value.Y);
        Close((ink.Left + ink.Right) / 2.0, item.Position!.Value.X);
    }

    [Fact]
    public void A_bar_is_answered_along_its_length()
    {
        var series = new BarSeries<Reading>
        {
            ItemsSource = [new Reading("a", 6)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Category,
            Fill = Brushes.Red,
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new CategoryAxis(),
        };

        chart.Series.Add(series);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        // Halfway along the bar, which is a place its value is not: the value is at the far end.
        var ink = InkOf(chart, series);
        var along = new Point((ink.Left + ink.Right) / 2.0, (ink.Top + ink.Bottom) / 2.0);

        Assert.Equal(series, Assert.Single(chart.FindDataPoints(along)).ChartItem.Series);
    }

    [Fact]
    public void A_column_too_narrow_to_draw_is_not_pointed_at()
    {
        // A column is a fraction of one axis unit, so a window measured in millions of units draws columns less
        // than a pixel wide - and a pixel grid that rounds them to nothing draws nothing. There is no ink there,
        // so there is nothing to point at: the answer used to be the reading's place on the axis, which is a tip
        // for a column nobody can see.
        var series = Column([5.0]);
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 1_000_000_000 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        chart.Series.Add(series);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        Assert.Equal(0, Ink.Count(InkBitmap(chart, series)));
        Assert.Empty(chart.FindDataPoints(new Point(chart.PlotBounds.X + 300, chart.PlotBounds.Bottom - 40)));
    }

    [Fact]
    public void A_region_holds_the_columns_it_covers()
    {
        var first = Column([5.0]);
        var second = Column([5.0]);
        var chart = Chart(SeriesLayout.Cluster, first, second);

        Paint(chart);

        var left = InkOf(chart, first);

        // The first series' own column, and nothing of the second's: they stand side by side, so a region over one
        // of them is over one of them.
        var over = new Rect(left.Left, left.Top, (left.Right - left.Left) / 2.0, left.Bottom - left.Top);

        Assert.Single(first.GetItemsInRegion(over));
        Assert.Empty(second.GetItemsInRegion(over));
    }

    [Fact]
    public void Only_the_series_laid_out_in_bands_share_a_slot()
    {
        var column = Column([5.0]);
        var line = new LineSeries<double> { ItemsSource = [1.0, 2.0], XSelector = value => value, YSelector = value => value };
        var chart = Chart(SeriesLayout.Cluster, column, line);

        var bands = chart.SeriesLayer.Bands;

        // The line takes no room from the columns: it is not in the layout at all, which is what makes a column
        // chart with a line over it work.
        Assert.NotNull(bands);
        Assert.Equal(1, bands.Count);
        Assert.Equal((0, 1), bands.PlaceOf(column));
    }

    [Fact]
    public void A_cluster_stands_side_by_side_within_the_slot()
    {
        var first = Column([5.0]);
        var second = Column([5.0]);
        var chart = Chart(SeriesLayout.Cluster, first, second);
        var plot = chart.PlotBounds;
        var left = InkOf(chart, first);
        var right = InkOf(chart, second);

        Assert.True(left.Right <= right.Left, $"the first stands at {left.Left}-{left.Right} and the second at {right.Left}-{right.Right}");
        Close(left.Right - left.Left, right.Right - right.Left);

        // The pair is centred on the slot, and between them they fill the fraction of the band one column would.
        Close(plot.X + (plot.Width / 2), (left.Left + right.Right) / 2.0);
        Close(plot.Width * ColumnSeries<double>.DefaultColumnWidthRatio, right.Right - left.Left);
    }

    [Fact]
    public void Stacking_adds_the_series_up()
    {
        var bottom = Column([3.0]);
        var top = Column([4.0]);
        var chart = Chart(SeriesLayout.Stacked, bottom, top);
        var plot = chart.PlotBounds;

        // The first column stands on zero and reaches three; the second stands on three and reaches seven.
        var under = InkOf(chart, bottom);

        Close(plot.Bottom, under.Bottom);
        Close(plot.Bottom - (plot.Height * 0.3), under.Top);

        var over = InkOf(chart, top);

        Close(plot.Bottom - (plot.Height * 0.3), over.Bottom);
        Close(plot.Bottom - (plot.Height * 0.7), over.Top);
    }

    [Fact]
    public void The_axis_of_a_stack_grows_to_its_totals()
    {
        var bottom = Column([3.0]);
        var top = Column([4.0]);
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis(),
            SeriesLayout = SeriesLayout.Stacked,
        };
        chart.Series.Add(bottom);
        chart.Series.Add(top);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        var range = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        // Zero, because the bottom column stands on it, and seven, because that is what the stack reaches.
        Assert.Equal(0, range.Minimum);
        Assert.Equal(7, range.Maximum);
    }

    [Fact]
    public void A_missing_value_in_a_stack_takes_no_height_and_keeps_its_mark()
    {
        var lower = new ColumnSeries<Reading>
        {
            ItemsSource = [new Reading("a", 3), new Reading("b", double.NaN), new Reading("c", 4)],
            XSelector = reading => reading.Category,
            YSelector = reading => reading.Value,
            Fill = Brushes.Red,
        };

        var upper = new ColumnSeries<Reading>
        {
            ItemsSource = [new Reading("a", 2), new Reading("b", 5), new Reading("c", 1)],
            XSelector = reading => reading.Category,
            YSelector = reading => reading.Value,
            Fill = Brushes.Red,
        };

        var chart = Chart(SeriesLayout.Stacked, lower, upper);
        var plot = chart.PlotBounds;
        var ink = InkOf(chart, upper);

        // The middle column of the upper series stands on zero: the record underneath it took no height, and the
        // stack did not shift to close the hole.
        Close(plot.Bottom, ink.Bottom);

        // And that record is still a record with nothing in it, rather than a zero: the tip can tell them apart.
        var missing = chart.GetAllDataPoints().Single(item => item.Series == lower && item.Index == 1);

        Assert.True(missing.Y.IsEmpty);
        Assert.Equal("b", missing.X.Category);
    }

    [Fact]
    public void A_column_stands_in_the_readings_own_spacing_rather_than_in_one_unit()
    {
        // The page whose own description promises "columns stacked under a line" had no columns on it at all: a
        // column stood in one unit of the band axis, and a unit of a date axis is a millisecond - so ten days of
        // readings came out as columns a thousandth of a pixel wide, which is to say as nothing, and the knob
        // that arranges them had nothing to arrange.
        var first = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var days = Enumerable.Range(0, 10).Select(day => first.AddDays(day)).ToArray();
        var series = new ColumnSeries<DateTimeOffset>
        {
            ItemsSource = days,
            XSelector = when => when,
            YSelector = _ => 5,
            Fill = Brushes.Red,
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new DateTimeAxis { Interval = TimeSpan.TicksPerDay / TimeSpan.TicksPerMillisecond },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        chart.Series.Add(series);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        var plot = chart.PlotBounds;
        var day = TimeSpan.TicksPerDay / TimeSpan.TicksPerMillisecond;

        // Half a day of room on each side of the readings, so that ten days are ten whole slots across the plot.
        var range = chart.Transform.GetLayout(ChartDimension.X).UnitRange;

        Close(day, range.Length / 10);
        Close(day / 2, days[0].ToUnixTimeMilliseconds() - range.Minimum);

        // Ten columns, each 0.65 of its own day-wide slot and half the plot tall.
        var bitmap = InkBitmap(chart, series);
        var column = plot.Width * ColumnSeries<double>.DefaultColumnWidthRatio / 10;
        var height = plot.Height / 2;

        Assert.Equal(10 * column * height, Ink.Count(bitmap), 20 * (column + height));
    }

    private static CartesianChart Chart(params Series[] series) => Chart(SeriesLayout.Overlay, series);

    private static CartesianChart Chart(SeriesLayout layout, params Series[] series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            SeriesLayout = layout,
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        if (layout == SeriesLayout.Cluster)
        {
            // The band layout is worked out during the layout pass, so a change of mode wants another one.
            chart.Measure(new Size(600, 400));
            chart.Arrange(new Rect(0, 0, 600, 400));
        }

        return chart;
    }

    /// <summary>One column of five on a category of its own.</summary>
    private static ColumnSeries<Reading> Column(double[] values) =>
        new()
        {
            ItemsSource = [.. values.Select((value, index) => new Reading($"c{index}", value))],
            XSelector = reading => reading.Category,
            YSelector = reading => reading.Value,
            Fill = Brushes.Red,
        };

    /// <summary>The ink one series leaves, painted on its own with the chart's own context.</summary>
    private static (int Left, int Top, int Right, int Bottom) InkOf(CartesianChart chart, Series series) =>
        Ink.Bounds(InkBitmap(chart, series));

    /// <summary>The middle of what a series drew, which is a place a pointer on it would be.</summary>
    private static Point MiddleOf(CartesianChart chart, Series series)
    {
        var ink = InkOf(chart, series);

        return new Point((ink.Left + ink.Right) / 2.0, (ink.Top + ink.Bottom) / 2.0);
    }

    /// <summary>Paints every series on a chart, so that each has been told where its band stands.</summary>
    /// <remarks>
    /// A pointer arrives between frames, and a series is told how its chart lays the bands out as it draws - so
    /// pointing at a chart is pointing at one that has been drawn.
    /// </remarks>
    private static void Paint(CartesianChart chart)
    {
        foreach (var series in chart.Series)
        {
            _ = InkOf(chart, series);
        }
    }

    /// <summary>Asserts that what was measured is what the geometry should have drawn, within a pixel or two.</summary>
    /// <remarks>
    /// A column's band edges are lined up with the pixel grid on purpose, and the ink a bitmap holds is whole
    /// pixels, so a measurement lands within about a pixel of the arithmetic. That is the tolerance, not a
    /// licence: a column twice as wide as it should be is nowhere near it.
    /// </remarks>
    private static void Close(double expected, double actual)
    {
        Assert.True(
            Math.Abs(expected - actual) <= 2,
            $"expected about {expected}, measured {actual}");
    }

    private static RenderTargetBitmap InkBitmap(CartesianChart chart, Series series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(600, 400));

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }

    /// <summary>Something with a category and a value, which is what a column series needs.</summary>
    private sealed record Reading(string Category, double Value);
}
