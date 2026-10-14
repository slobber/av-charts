using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the data tips: the box that appears beside the reading a pointer is on, the modes that turn it off or
/// make it follow the pointer, the line a host can write instead, and the stack that keeps every tip in view from
/// covering its neighbours.
/// </summary>
/// <remarks>
/// The charts here wear the theme, because a tip is a control: with no theme it has no template, and a box with
/// no template has no size and writes nothing. That is the arrangement a host has, so it is the honest one to
/// measure against. The tips are driven through <c>ShowDataTipsAt</c> and <c>HideDataTips</c> rather than through
/// synthesised pointer input, for the reason the gesture and crosshair tests give: routing is the one part of
/// this that the headless platform cannot exercise.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class DataTipTests
{
    private const int Width = 400;
    private const int Height = 200;

    [Fact]
    public void A_tip_stands_beside_the_reading_the_pointer_is_on()
    {
        var (chart, series, _) = Chart();
        var at = Position(series, 1);

        chart.Show(at);

        var tip = Assert.Single(Tips(chart));
        var box = Placed(tip);

        // It writes what the axes wrote for that record, and it carries the hit it is about.
        Assert.Equal("2, 2", tip.Text);
        Assert.Equal(1, tip.Data!.Value.ChartItem.Index);

        // It is a box rather than a point - it was measured, which is what says the theme reached it - and it
        // stands to the right of the reading and above it.
        Assert.True(box.Width > 0 && box.Height > 0, $"the tip measured {box.Size}");
        Assert.True(box.Left >= at.X, $"the tip is at {box.Left} for a reading at {at.X}");
        Assert.True(box.Bottom <= at.Y, $"the tip is at {box.Bottom} for a reading at {at.Y}");
        Assert.True(new Rect(0, 0, Width, Height).Contains(box), $"the tip is at {box} on a {Width}x{Height} chart");

        // And it goes when the pointer does.
        chart.Hide();

        Assert.Empty(Tips(chart));
    }

    [Fact]
    public void There_is_no_tip_where_there_is_no_reading()
    {
        var (chart, _, _) = Chart(vertical: value => value == 2 ? ChartValue.Empty : value);

        // Nowhere near a record: a tip is a reading, so there has to be one.
        chart.Show(new Point(chart.PlotBounds.Right - 2, chart.PlotBounds.Top + 2));

        Assert.Empty(Tips(chart));

        // A record with no value has no place on the chart, so it cannot be pointed at either - which is the
        // design brief's rule that a tip skips a gap.
        chart.Show(Pixel(chart, 2, 2));

        Assert.Empty(Tips(chart));

        // Its neighbour can still be pointed at, so that is about the gap rather than about the chart.
        chart.Show(Position(chart.Series[0], 0));

        Assert.Single(Tips(chart));
    }

    [Fact]
    public void The_tips_can_be_turned_off_or_made_to_follow_the_pointer()
    {
        var (chart, series, _) = Chart();
        var at = Position(series, 1);

        chart.Show(at);

        var beside = Placed(Assert.Single(Tips(chart)));

        // Following the pointer: the same reading, but the tip is about the pointer rather than about the record,
        // so it stands a little further across the chart.
        chart.DataTipMode = DataTipMode.Mouse;
        chart.Show(at + new Point(3, 3));

        var following = Placed(Assert.Single(Tips(chart)));

        Assert.True(following.Left > beside.Left, $"following put it at {following.Left}, beside at {beside.Left}");

        // And none at all, which is the mode a chart asks for when it wants no tips.
        chart.DataTipMode = DataTipMode.None;

        Assert.Empty(Tips(chart));
    }

    [Fact]
    public void Asking_for_every_tip_lays_them_out_without_covering_each_other()
    {
        // Six readings on top of one another: without stacking, six boxes would pile up in the same few pixels.
        var (chart, _, _) = Chart([2.0, 2.02, 2.04, 2.06, 2.08, 2.1]);

        chart.ShowAllDataTips = true;

        var tips = Tips(chart);
        var boxes = tips.Select(Placed).ToArray();

        Assert.Equal(6, tips.Length);

        foreach (var box in boxes)
        {
            Assert.True(box.Width > 0 && box.Height > 0, $"a tip measured {box.Size}, so the theme did not reach it");
            Assert.True(new Rect(0, 0, Width, Height).Contains(box), $"a tip is at {box}, off a {Width}x{Height} chart");

            foreach (var other in boxes.Where(other => other != box))
            {
                Assert.False(box.Intersects(other), $"these two tips cover each other: {box} and {other}");
            }
        }

        // One tip per reading, each writing its own: the six records are told apart by their horizontal value.
        Assert.Equal(6, tips.Select(tip => tip.Text).Distinct().Count());

        // Unlike the pointer's tip, these are up with no pointer anywhere near the chart - which is what asking
        // for all of them means - and each of them is a reading.
        Assert.All(tips, tip => Assert.NotNull(tip.Data));
    }

    [Fact]
    public void Asking_for_every_tip_gets_one_for_every_reading_of_every_series()
    {
        // Two series, six readings each: "every tip" means twelve of them. It used to mean one - the property was
        // read as if a chart had a single series, which a scatter with a bubble per reading is not.
        var values = new[] { 1.0, 2.0, 3.0, 4.0, 1.5, 2.5 };
        var (chart, series, _) = Chart(values);

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = values,
            XSelector = value => value,
            YSelector = value => value,
            Marker = Markers.Circle,
        });

        chart.ShowAllDataTips = true;

        var tips = Tips(chart);

        Assert.Equal(2 * values.Length, tips.Length);
        Assert.All(tips, tip => Assert.NotNull(tip.Data));

        // And the lines tell them apart: the same six readings under two names.
        Assert.All(tips, tip => Assert.False(string.IsNullOrWhiteSpace(tip.Text)));
    }

    [Fact]
    public void A_scatter_and_a_bubble_per_reading_each_get_a_tip()
    {
        // The shape the points page has: readings whose horizontal value is not in the order they are listed,
        // drawn as marks and as bubbles. Every one of them is a reading, so every one of them gets a tip - and
        // this is the shape that used to get one, because a series that is not in order was taken for one that is
        // and the hit test looked for its readings in the wrong place.
        // Multiplied by numbers that have no common divisor with the count, so that no two readings share a place
        // and none of them is where the one before it was: that is what makes the list out of order.
        var readings = Enumerable.Range(0, 24)
            .Select(index => new Spot(0.1 + ((index * 5) % 24) * 0.15, 0.3 + ((index * 7) % 24) * 0.14))
            .ToArray();

        var (chart, _, _) = Chart();

        chart.Series.Clear();

        chart.Series.Add(new PlotSeries<Spot>
        {
            ItemsSource = readings,
            XSelector = reading => reading.X,
            YSelector = reading => reading.Y,
            Marker = Markers.Circle,
            MarkerSize = 3,
        });

        chart.Series.Add(new BubbleSeries<Spot>
        {
            ItemsSource = readings,
            XSelector = reading => reading.X + 0.25,
            YSelector = reading => reading.Y,
            RadiusSelector = reading => 3,
            Marker = Markers.Circle,
        });

        chart.ShowAllDataTips = true;

        Assert.Equal(48, Tips(chart).Length);
    }

    [Fact]
    public void A_host_can_write_the_tip_itself()
    {
        var (chart, series, _) = Chart();

        chart.DataTipFunction = hit => $"#{hit.ChartItem.Index} {hit.Item}";
        chart.Show(Position(series, 2));

        Assert.Equal("#2 3", Assert.Single(Tips(chart)).Text);
    }

    [Fact]
    public void The_theme_dresses_the_tips_and_a_host_can_dress_them_differently()
    {
        var (chart, series, themed) = Chart();

        chart.Show(Position(series, 1));

        var tip = Assert.Single(Tips(chart));

        Assert.Same(themed.Value(ChartTokens.DataTipBackground, ThemeVariant.Light), tip.Background);
        Assert.Same(themed.Value(ChartTokens.DataTipBorder, ThemeVariant.Light), tip.BorderPen);
        Assert.Same(themed.Value(ChartTokens.DataTipForeground, ThemeVariant.Light), tip.Foreground);

        // A host's own theme under the same key is what the tips wear instead - and that is the whole of how a tip
        // is reskinned or retemplated: no property of the chart is involved, and the host never sees the tips the
        // chart makes.
        var mine = new ControlTheme(typeof(DataTip))
        {
            BasedOn = (ControlTheme)themed.Theme[typeof(DataTip)]!,
            Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.HotPink) },
        };

        var (dressed, dressedSeries, _) = Chart(
            overrides: new ResourceDictionary { [typeof(DataTip)] = mine });

        dressed.Show(Position(dressedSeries, 1));

        var reskinned = Assert.Single(Tips(dressed));

        Assert.Same(Brushes.HotPink, reskinned.Background);
        Assert.True(Placed(reskinned).Width > 0, "the host's theme should have kept the template through BasedOn");
    }

    /// <summary>A dressed chart of the size these tests measure against, with one series on it.</summary>
    /// <param name="values">The values to draw.</param>
    /// <param name="vertical">How a value becomes a vertical reading, when the default is not what is wanted.</param>
    /// <param name="overrides">Resources for the chart itself, as a host with a theme of its own would set.</param>
    /// <param name="marker">Whether the series draws a mark at each reading.</param>
    /// <returns>The chart, its series, and the theme it is wearing.</returns>
    private static (ChartProbe Chart, LineSeries<double> Series, ThemedChart Theme) Chart(
        double[]? values = null,
        Func<double, ChartValue>? vertical = null,
        ResourceDictionary? overrides = null,
        bool marker = false)
    {
        var themed = new ThemedChart(overrides, () => new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
        });

        var series = new LineSeries<double>
        {
            ItemsSource = values ?? [1.0, 2.0, 3.0],
            XSelector = value => value,
            YSelector = vertical ?? (value => value),
            Marker = marker ? Markers.Circle : null,
        };

        var chart = (ChartProbe)themed.Chart;
        chart.Series.Add(series);
        themed.Layout();

        return (chart, series, themed);
    }

    [Fact]
    public void A_tip_at_a_crossing_writes_both_readings()
    {
        // Two lines crossing are two readings, and a reader pointing at the crossing is asking about both: one
        // tip, one line of text per series, the way a tip of its own would write each of them.
        var (chart, _, _) = Chart([1.0, 2.0, 3.0]);

        var second = new LineSeries<double>
        {
            DisplayName = "Speed",
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => value,
            YSelector = value => value,
        };

        chart.Series.Add(second);
        chart.Show(Pixel(chart, 2, 2));

        var tip = Assert.Single(Tips(chart));
        var lines = tip.Text!.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, line => line == "2, 2");
        Assert.Contains(lines, line => line == "Speed: 2, 2");

        // And one tip rather than two: the tip stands where the readings are, and its own height has room for
        // both lines.
        Assert.True(Placed(tip).Height > 20, $"the tip is {Placed(tip).Height} tall, so it is one line");
    }
    /// <summary>The tips that are up, found the way anything finds a control: in the visual tree.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The tips.</returns>
    private static DataTip[] Tips(CartesianChart chart) =>
        [.. chart.GetVisualDescendants().OfType<DataTip>().Where(tip => tip.IsVisible)];

    /// <summary>The rectangle a tip was put in: where the layout put it, and how much room it asked for.</summary>
    /// <param name="tip">The tip.</param>
    /// <returns>The rectangle, in the chart's own coordinates.</returns>
    private static Rect Placed(DataTip tip) =>
        new(Canvas.GetLeft(tip), Canvas.GetTop(tip), tip.DesiredSize.Width, tip.DesiredSize.Height);

    /// <summary>Where a record is drawn, which is where a pointer has to be for a tip to be about it.</summary>
    /// <param name="series">The series to look in.</param>
    /// <param name="index">The record's position in the data.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Position(Series series, int index) =>
        series.Items.Single(item => item.Index == index).Position!.Value;

    /// <summary>Where a pair of values is drawn on a chart whose axes both run from zero to four.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Pixel(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 4 * plot.Width), plot.Bottom - (y / 4 * plot.Height));
    }

    /// <summary>A reading with a place of its own on both dimensions.</summary>
    private sealed record Spot(double X, double Y);

    /// <summary>A chart that can be driven the way its pointer handlers drive it.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Show(Point at) => ShowDataTipsAt(at);

        public void Hide() => HideDataTips();
    }
}
