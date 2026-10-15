using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the chart of shares: that a reading is worth the slice of the circle it earns, that a reading with
/// no value draws no wedge and is in no total, that a ring leaves the middle of the circle empty, and that a
/// pointer finds the wedge it is on.
/// </summary>
/// <remarks>
/// As with a candle, the evidence is the ink: the series is painted on its own into a bitmap and what it drew
/// is measured, and where a wedge is asked about it is asked through the hit test - which is the same
/// arithmetic a reader's pointer goes through.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class PieSeriesTests
{
    [Fact]
    public void A_reading_is_the_share_of_the_circle_it_is_worth()
    {
        var chart = Chart([1.0, 3.0], out var series);

        Assert.Equal(4, series.Total);
        Assert.Equal(0.25, series.ShareOf(0), 9);
        Assert.Equal(0.75, series.ShareOf(1), 9);
        Assert.Equal(2, series.Count);

        // And the two of them are the whole circle: what is drawn covers the disc the plot holds, which is the
        // largest circle that fits it - a radius of a hundred and fifty in a plot of four hundred by three.
        var circle = Math.PI * 150 * 150;
        var drawn = Ink.Count(Paint(chart, series));

        Assert.True(
            drawn > circle * 0.95 && drawn < circle * 1.05,
            $"{drawn} pixels of ink for a circle of {circle:0}");
    }

    [Fact]
    public void A_reading_with_no_value_is_no_wedge_and_no_share()
    {
        // Three readings, the middle one missing: the two that are left are the whole of the circle, and the
        // missing one is in no denominator - which is the null-value rule the rest of the library follows.
        var chart = Chart([1.0, double.NaN, 3.0], out var series);

        Assert.Equal(4, series.Total);
        Assert.Equal(0, series.ShareOf(1));

        // And it is not drawn: no angle of the circle finds it, while the two that are worth something are
        // found where they stand.
        Assert.Equal(0, Hit(chart, series, 0.1)!.Value.ChartItem.Index);
        Assert.Equal(2, Hit(chart, series, 0.5)!.Value.ChartItem.Index);

        foreach (var turns in new[] { 0.0, 0.05, 0.2, 0.5, 0.9 })
        {
            Assert.NotEqual(1, Hit(chart, series, turns)?.ChartItem.Index);
        }
    }

    [Fact]
    public void A_reading_less_than_nothing_is_no_wedge_rather_than_a_wedge_the_other_way()
    {
        // Flex adds a negative reading to the total and draws it as a wedge that reaches back the way the
        // others came, which is a share of a circle no reader can be shown. A reading that is worth less than
        // nothing instead draws nothing and takes no part in the whole: the pie goes on being a pie.
        var chart = Chart([1.0, -5.0, 3.0], out var series);

        Assert.Equal(4, series.Total);
        Assert.Equal(0, series.ShareOf(1));
        Assert.Equal(0.25, series.ShareOf(0), 9);
        Assert.Equal(0.75, series.ShareOf(2), 9);

        foreach (var turns in new[] { 0.0, 0.3, 0.6, 0.9 })
        {
            Assert.NotEqual(1, Hit(chart, series, turns)?.ChartItem.Index);
        }
    }

    [Fact]
    public void The_wedge_under_the_pointer_is_the_one_it_is_on()
    {
        // Four equal readings: a quarter of the circle each, running anticlockwise from three o'clock.
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        Assert.Equal(0, Hit(chart, series, 0.125)!.Value.ChartItem.Index);
        Assert.Equal(1, Hit(chart, series, 0.375)!.Value.ChartItem.Index);
        Assert.Equal(2, Hit(chart, series, 0.625)!.Value.ChartItem.Index);
        Assert.Equal(3, Hit(chart, series, 0.875)!.Value.ChartItem.Index);

        // The join between two wedges belongs to the one that starts there, and three o'clock is a join.
        Assert.Equal(0, Hit(chart, series, 0)!.Value.ChartItem.Index);
        Assert.Equal(1, Hit(chart, series, 0.25)!.Value.ChartItem.Index);

        // And a pointer off the circle is on no wedge at all, however far round it is.
        Assert.Null(series.HitTestItem(Off(chart, 0.125, 1.4), 2));
    }

    [Fact]
    public void A_ring_leaves_the_middle_of_the_circle_empty()
    {
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);
        var middle = Around(chart, 0, 0);

        Assert.True(Ink.Count(Paint(chart, series), middle) > 0, "a whole pie covers its own middle");

        series.InnerRadius = 0.5;

        Assert.Equal(0, Ink.Count(Paint(chart, series), middle));
        Assert.True(Ink.Count(Paint(chart, series)) > 0, "a ring is still drawn");
    }

    [Fact]
    public void A_pushed_out_pie_opens_up_between_its_wedges()
    {
        // Every wedge moves out along its own middle, which is what a reader calls a pushed-out pie: the wedges
        // stop touching, the middle of the circle is left empty, and the ring reaches past where the rim was.
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        Assert.True(Ink.Count(Paint(chart, series), Around(chart, 0, 0)) > 0, "it starts as a whole pie");

        // A place just off the rim is on no wedge while the ring is where the circle put it.
        Assert.Null(series.HitTestItem(PointAt(chart, 0.125, 1.05), 2));

        series.ExplodeRadius = 0.1;

        var picture = Paint(chart, series);

        Assert.Equal(0, Ink.Count(picture, Around(chart, 0, 0)));
        Assert.True(
            Ink.Bounds(picture).Right - Ink.Bounds(picture).Left > 300,
            $"a ring pushed out by a tenth of a hundred and fifty reaches past three hundred: {Ink.Bounds(picture)}");

        // And the pointer follows the wedge: a place that used to be off the rim is on the wedge that moved out
        // to meet it.
        Assert.Equal(0, series.HitTestItem(PointAt(chart, 0.125, 1.05), 2)!.Value.ChartItem.Index);
    }

    [Fact]
    public void One_wedge_can_be_asked_to_stand_out_on_its_own()
    {
        // Flex's perWedgeExplodeRadius, which is how a pie points at the slice somebody is asking about.
        var chart = Chart([1.0, 2.0, 3.0, 4.0], out var series);

        // The reading worth four tenths of the whole is the one asked to stand out, and it is the last of the
        // four, so it is drawn from six tenths of a turn to three o'clock: its middle is at eight tenths.
        series.ExplodeSelector = item => item == 4 ? 0.3 : 0;

        Assert.Equal(3, series.HitTestItem(PointAt(chart, 0.8, 1.25), 2)!.Value.ChartItem.Index);

        foreach (var turns in new[] { 0.05, 0.15, 0.35 })
        {
            Assert.Null(series.HitTestItem(PointAt(chart, turns, 1.25), 2));
        }
    }

    [Fact]
    public void A_chart_of_one_reading_is_the_whole_circle_rather_than_nothing()
    {
        // A wedge that goes all the way round is an arc from a point back to itself, which is no arc at all -
        // so it is drawn as the two circles that make it a ring, and the even-odd rule leaves the hole.
        var chart = Chart([7.0], out var series);

        Assert.Equal(1, series.ShareOf(0));
        Assert.True(Ink.Count(Paint(chart, series), Around(chart, 0, 0)) > 0);

        series.InnerRadius = 0.5;

        Assert.Equal(0, Ink.Count(Paint(chart, series), Around(chart, 0, 0)));
    }

    [Fact]
    public void The_wedges_run_the_way_they_are_asked_to()
    {
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        // Anticlockwise, as Flex draws a pie and as this series does by default: the first quarter is the one
        // above three o'clock.
        Assert.Equal(0, Hit(chart, series, 0.125)!.Value.ChartItem.Index);

        series.Clockwise = true;

        // Clockwise it is the one below, and the quarters swap over.
        Assert.Equal(0, Hit(chart, series, 0.875)!.Value.ChartItem.Index);
        Assert.Equal(3, Hit(chart, series, 0.125)!.Value.ChartItem.Index);
    }

    [Fact]
    public void Where_the_first_wedge_starts_is_where_the_host_says()
    {
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        // Ninety degrees is twelve o'clock, so the quarters run up, left, down and right from there.
        series.StartAngle = 90;

        Assert.Equal(0, Hit(chart, series, 0.375)!.Value.ChartItem.Index);
        Assert.Equal(1, Hit(chart, series, 0.625)!.Value.ChartItem.Index);
        Assert.Equal(2, Hit(chart, series, 0.875)!.Value.ChartItem.Index);
        Assert.Equal(3, Hit(chart, series, 0.125)!.Value.ChartItem.Index);
    }

    [Fact]
    public void The_tip_names_the_wedge_and_says_how_much_of_the_circle_it_is()
    {
        var chart = Chart([1.0, 3.0], out var series, name: value => $"Wedge {value}");

        var hit = Hit(chart, series, 0.125);

        Assert.NotNull(hit);
        Assert.Equal("Wedge 1: 1 (25%)", hit!.Value.DisplayText);

        // And a reading nobody named is named by where it stands rather than left blank.
        var unnamed = Chart([1.0, 3.0], out var noNames);

        Assert.StartsWith("Wedge 1:", Hit(unnamed, noNames, 0.125)!.Value.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_wedge_takes_a_colour_of_its_own_from_the_palette()
    {
        // The palette is the chart's, and a pie is the one series that needs all of it: every wedge is a colour
        // of its own, taken in the order the readings came in.
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        var picture = Paint(chart, series);
        var first = Colour(ChartTokens.SeriesFill(1));
        var second = Colour(ChartTokens.SeriesFill(2));

        Assert.True(
            Ink.Count(picture, Painted(first), Around(chart, 0.125, 0.75)) > 0,
            "the first wedge is the first colour");
        Assert.True(
            Ink.Count(picture, Painted(second), Around(chart, 0.375, 0.75)) > 0,
            "the second wedge is the second colour");
    }

    [Fact]
    public void The_series_asks_the_chart_for_nothing_to_cover()
    {
        // A pie is shares of its own total rather than readings on a scale, so it tells an axis nothing - and a
        // chart of one has no axes to tell.
        var chart = Chart([1.0, 3.0], out var series);

        Assert.Empty(chart.Transform.AxesInPlay);
        Assert.Equal(AxisLayout.Empty, chart.Transform.GetLayout(ChartDimension.Y));

        // And the plot is the whole of what the chart was given, because there are no labels to make room for.
        Assert.Equal(new Rect(0, 0, 400, 300), chart.PlotBounds);

        _ = series;
    }

    [Fact]
    public void A_wedge_is_written_on_when_the_wedge_has_room_for_the_words()
    {
        // A label inside a wedge is drawn when the wedge can hold it and left out when it cannot: a word spilling
        // out of its own slice is worse than no word, and a pie whose labels are each a different size reads as a
        // mistake - so this drops rather than shrinks, which is where it parts company with Flex.
        var chart = Chart([1.0, 99.0], out var series);

        series.LabelSelector = value => value.ToString(CultureInfo.InvariantCulture);
        series.LabelPosition = PieLabelPosition.Inside;

        var ink = Colour(ChartTokens.AxisLabelForeground);
        var picture = Paint(chart, series);

        // The reading worth ninety-nine of the hundred is written in the middle of its own wedge, and the sliver
        // worth one is not written at all - its mouth is four pixels across where the text stands.
        Assert.True(Ink.Count(picture, ink, Around(chart, 0.505, 0.5)) > 0, "the wide wedge is written on");
        Assert.Equal(0, Ink.Count(picture, ink, Around(chart, 0.005, 0.5)));
    }

    [Fact]
    public void A_label_beside_the_circle_has_a_line_that_reaches_back_to_its_wedge()
    {
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        series.LabelSelector = value => "North";

        var ink = Colour(ChartTokens.AxisLabelForeground);

        // Nothing is written unless a host asks for it, and then it is written beside the circle - in the room
        // the plot has on either side of it, which is where the line from the wedge reaches.
        Assert.Equal(0, Ink.Count(Paint(chart, series), ink));

        series.LabelPosition = PieLabelPosition.Outside;

        var picture = Paint(chart, series);

        Assert.True(Ink.Count(picture, ink, Room(right: true)) > 0, "a label beside the circle is written beside it");
        Assert.True(Ink.Count(picture, ink, Room(right: false)) > 0, "and one on the other side of it too");
    }

    [Fact]
    public void A_label_that_cannot_be_got_onto_the_picture_is_left_off_it()
    {
        // The same rule the axis labels follow: what cannot be drawn whole is not drawn. A word forty characters
        // long has nowhere to stand beside this circle, so no label ink goes onto the picture at all - and the
        // wedges are drawn just the same, which is what makes it a dropped label rather than a broken pie.
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out var series);

        series.LabelSelector = value => new string('w', 40);
        series.LabelPosition = PieLabelPosition.Outside;

        var ink = Colour(ChartTokens.AxisLabelForeground);
        var picture = Paint(chart, series);

        Assert.Equal(0, Ink.Count(picture, ink));
        Assert.True(Ink.Count(picture) > 0, "the wedges are drawn whether or not their labels are");
    }

    /// <summary>The room a plot has on one side of the circle, which is where a label beside it is written.</summary>
    /// <param name="right">Which side.</param>
    /// <returns>The rectangle beside the circle, which is where a label beside it is written.</returns>
    private static Rect Room(bool right) => right ? new Rect(310, 0, 90, 300) : new Rect(0, 0, 90, 300);
    [Fact]
    public void Two_pies_are_two_rings_rather_than_two_pies_on_top_of_each_other()
    {
        // Nested pies: a chart of two series divides the circle between them, which is what Flex's own pie chart
        // does and the only way two pies can be read at once. The inner one is drawn inside the outer one, and
        // the point half way out belongs to the inner of the two.
        var chart = new PieChart();
        var inner = new PieSeries<double>
        {
            ItemsSource = [1.0, 1.0],
            ValueSelector = value => value,
            DisplayName = "This year",
        };

        var outer = new PieSeries<double>
        {
            ItemsSource = [3.0, 1.0],
            ValueSelector = value => value,
            DisplayName = "Last year",
        };

        chart.Series.Add(inner);
        chart.Series.Add(outer);
        _ = new Window { Resources = ChartTheme.Create(), Content = chart };
        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));

        // The inner series is the first, so it takes the ring from the middle to half way out: the ink of its
        // own picture is a disc of half the circle, and the outer one's is a ring that leaves the middle empty.
        var innerInk = Paint(chart, inner);
        var outerInk = Paint(chart, outer);
        var middle = Around(chart, 0, 0);
        var far = Around(chart, 0.125, 0.75);

        Assert.True(Ink.Count(innerInk, middle) > 0, "the inner pie covers the middle");
        Assert.Equal(0, Ink.Count(outerInk, middle));
        Assert.Equal(0, Ink.Count(innerInk, far));
        Assert.True(Ink.Count(outerInk, far) > 0, "the outer pie is drawn out at the rim");

        // And a pointer half way out is on a wedge of the inner pie, not of the outer one.
        Assert.Same(inner, chart.FindDataPoints(PointAt(chart, 0.125, 0.4)).Single().ChartItem.Series);
        Assert.Same(outer, chart.FindDataPoints(PointAt(chart, 0.125, 0.8)).Single().ChartItem.Series);
    }

    [Fact]
    public void The_chart_finds_the_wedge_the_pointer_is_on()
    {
        // The other way into a chart of shares: not the series' own hit test but the one a pointer, a crosshair
        // or a data tip goes through - which is the same arithmetic reached by a longer road.
        var chart = Chart([1.0, 1.0, 1.0, 1.0], out _);

        Assert.Equal(1, chart.FindDataPoints(PointAt(chart, 0.375, 0.75)).Single().ChartItem.Index);
        Assert.Equal(2, chart.FindDataPoints(PointAt(chart, 0.625, 0.9)).Single().ChartItem.Index);

        // A pointer outside the circle is on nothing at all.
        Assert.Empty(chart.FindDataPoints(Off(chart, 0.375, 2.0)));
    }

    /// <summary>A chart of one pie series, measured and arranged, with the whole of itself for a plot.</summary>
    /// <param name="values">What each reading is worth.</param>
    /// <param name="series">The series, for the test to turn knobs on.</param>
    /// <param name="name">What a reading is called, when the test cares.</param>
    /// <returns>The chart.</returns>
    private static PieChart Chart(
        double[] values,
        out PieSeries<double> series,
        Func<double, string?>? name = null)
    {
        var chart = new PieChart();

        series = new PieSeries<double>
        {
            ItemsSource = values,
            ValueSelector = value => value,
            NameSelector = name,
            DisplayName = "Shares",
        };

        chart.Series.Add(series);

        // Dressed, so that the wedges have the chart's palette to be drawn in: a series painted without one has
        // no colour to draw with, which is what every other series in the library does too.
        _ = new Window { Resources = ChartTheme.Create(), Content = chart };

        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));
        return chart;
    }

    /// <summary>Paints the series on its own into a picture, which is where the evidence is.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="series">The series.</param>
    /// <returns>What it drew.</returns>
    private static RenderTargetBitmap Paint(Control chart, Series series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 300));
        var owning = (PieChart)chart;

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(owning.Transform, owning.PlotBounds, 1, owning.SeriesLayer.Bands));
        }

        return bitmap;
    }

    /// <summary>Asks what wedge the pointer would be on if it were at an angle, half way out.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="series">The series.</param>
    /// <param name="turns">The angle, in turns anticlockwise from three o'clock.</param>
    /// <returns>The hit, or null.</returns>
    private static HitData? Hit(Control chart, Series series, double turns) =>
        series.HitTestItem(PointAt(chart, turns, 0.75), 2);

    /// <summary>Where an angle and a radius are on the chart.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="turns">The angle, in turns.</param>
    /// <param name="reach">How far out, as a fraction of the circle's radius.</param>
    /// <returns>The position.</returns>
    private static Point PointAt(Control chart, double turns, double reach)
    {
        var plot = ((PieChart)chart).PlotBounds;
        var middle = plot.Center;
        var radius = Math.Min(plot.Width, plot.Height) / 2 * reach;
        var radians = turns * 2 * Math.PI;

        return new Point(middle.X + (Math.Cos(radians) * radius), middle.Y - (Math.Sin(radians) * radius));
    }

    /// <summary>A small box around a place on the circle, for counting the ink of one wedge.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="turns">The angle, in turns.</param>
    /// <param name="reach">How far out, as a fraction of the circle's radius.</param>
    /// <returns>The box.</returns>
    private static Rect Around(Control chart, double turns, double reach)
    {
        var at = PointAt(chart, turns, reach);

        return new Rect(at.X - 3, at.Y - 3, 6, 6);
    }

    /// <summary>Where a place off the circle is, which is what a pointer at nothing looks like.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="turns">The angle, in turns.</param>
    /// <param name="reach">How far out, as a multiple of the circle's radius.</param>
    /// <returns>The position.</returns>
    private static Point Off(Control chart, double turns, double reach) => PointAt(chart, turns, reach);

    /// <summary>The colour of a palette token, read out of the theme: a pen's ink, or a brush's own colour.</summary>
    /// <param name="token">The token's name.</param>
    /// <returns>The colour.</returns>
    private static Color Colour(string token)
    {
        var theme = ChartTheme.Create();

        Assert.True(theme.TryGetResource(token, ThemeVariant.Light, out var value), $"{token} is not defined");

        return value switch
        {
            Pen pen => ((ISolidColorBrush)pen.Brush!).Color,
            ISolidColorBrush brush => brush.Color,
            _ => throw new InvalidOperationException($"{token} is neither a pen nor a brush"),
        };
    }

    /// <summary>The colour a brush leaves in a picture that has nothing behind it.</summary>
    /// <param name="colour">The colour the brush is.</param>
    /// <returns>The colour of the pixels it painted.</returns>
    /// <remarks>
    /// A picture hands its pixels back with their own transparency already counted in, so a fill held back to
    /// forty-seven per cent is a smaller number than the colour it was made from - and a test that looks for the
    /// colour itself finds nothing at all, which is exactly what this one did first.
    /// </remarks>
    private static Color Painted(Color colour) => Color.FromArgb(
        colour.A,
        (byte)(((colour.R * colour.A) + 127) / 255),
        (byte)(((colour.G * colour.A) + 127) / 255),
        (byte)(((colour.B * colour.A) + 127) / 255));
}
