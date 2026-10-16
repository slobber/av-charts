using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the two sections: the band across the plot between two readings, and the slab along an axis between two
/// readings - including the rule that ties them together, that a named axis section takes over what is written along
/// its stretch.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class SectionTests
{
    private static readonly Color Band = Color.FromRgb(0x46, 0x82, 0xB4);
    private static readonly Color Slab = Color.FromRgb(0xB2, 0x22, 0x22);

    [Fact]
    public void A_section_is_a_band_between_two_readings_across_the_whole_plot()
    {
        var chart = Laid(Chart());

        chart.Elements.Add(new Section
        {
            Dimension = ChartDimension.Y,
            From = 4,
            To = 6,
            Fill = new SolidColorBrush(Band),
        });

        var (left, top, right, bottom) = Ink.Bounds(Picture(chart));

        Assert.True(Ink.Count(Picture(chart), Band, 20) > 0, "the band was not drawn");
        Near(chart.PlotBounds.Left, left);
        Near(chart.PlotBounds.Right, right);
        Near(Up(chart, 6), top);
        Near(Up(chart, 4), bottom);
    }

    [Fact]
    public void A_section_with_one_end_left_out_reaches_the_edge_of_the_plot()
    {
        // "Everything above this": the host does not have to know where the plot ends to say so.
        var chart = Laid(Chart());

        chart.Elements.Add(new Section
        {
            Dimension = ChartDimension.Y,
            From = 6,
            Fill = new SolidColorBrush(Band),
        });

        var (_, top, _, bottom) = Ink.Bounds(Picture(chart));

        Near(chart.PlotBounds.Top, top);
        Near(Up(chart, 6), bottom);
    }

    [Fact]
    public void A_section_on_the_other_dimension_stands_up_the_plot_instead()
    {
        var chart = Laid(Chart());

        chart.Elements.Add(new Section
        {
            Dimension = ChartDimension.X,
            From = 2,
            To = 4,
            Fill = new SolidColorBrush(Band),
        });

        var (left, top, right, bottom) = Ink.Bounds(Picture(chart));

        Near(Across(chart, 2), left);
        Near(Across(chart, 4), right);
        Near(chart.PlotBounds.Top, top);
        Near(chart.PlotBounds.Bottom, bottom);
    }

    [Fact]
    public void A_section_a_reading_cannot_be_placed_by_draws_nothing()
    {
        // A section nobody can place is a mistake rather than a reading, so it is not half drawn: the band that goes
        // nowhere is the whole of it left out.
        var chart = Laid(Chart());

        chart.Elements.Add(new Section
        {
            Dimension = ChartDimension.Y,
            From = ChartValue.FromCategory("nowhere"),
            To = 6,
            Fill = new SolidColorBrush(Band),
        });

        Assert.Equal(0, Ink.Count(Picture(chart), Band, 20));
    }

    [Fact]
    public void A_section_is_named_where_it_starts()
    {
        var chart = Laid(Chart());
        var from = Across(chart, 3);
        var to = Across(chart, 6);

        chart.Elements.Add(new Section
        {
            Dimension = ChartDimension.X,
            From = 3,
            To = 6,
            Name = "released",
            NameBrush = Brushes.Black,
        });

        var picture = Picture(chart);
        var (left, top, _, bottom) = Ink.Bounds(picture);

        Assert.True(Ink.Count(picture) > 0, "the name was not drawn");
        Assert.InRange(left, from, to);

        // One line of writing at the top of the band, which is where the name goes: the ink starts a few pixels below
        // the top of the text's own box, the way any line of writing does.
        Assert.InRange(top, chart.PlotBounds.Top - 1, chart.PlotBounds.Top + 20);
        Assert.True(bottom < chart.PlotBounds.Bottom - 1, "the name should be one line of writing");
    }

    [Fact]
    public void A_named_axis_section_takes_over_its_stretch_of_the_labels()
    {
        // Which is the whole reason to name one: a name written over three labels is a name nobody reads.
        var axis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 1 };
        var renderer = new AxisRenderer(axis, ChartDimension.X);
        var layout = axis.Resolve([], 400);
        var plot = new Rect(0, 0, 400, 100);
        var before = renderer.GetTicks(layout, plot).Select(tick => tick.Position).ToArray();

        renderer.AddSection(new AxisSection { From = 4, To = 6, Name = "released" });

        var after = renderer.GetTicks(layout, plot).Select(tick => tick.Position).ToArray();
        var (low, high) = Ends(axis, layout, plot, 4, 6);

        Assert.True(before.Length > 4, $"{before.Length} labels to start with");
        Assert.True(after.Length < before.Length, "the labels inside the section are still there");
        Assert.DoesNotContain(after, at => at > low && at < high);
        Assert.Contains(after, at => at < low);
        Assert.Contains(after, at => at > high);
    }

    [Fact]
    public void An_unnamed_axis_section_leaves_the_labels_where_they_are()
    {
        var axis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 1 };
        var renderer = new AxisRenderer(axis, ChartDimension.X);
        var layout = axis.Resolve([], 400);
        var plot = new Rect(0, 0, 400, 100);
        var before = renderer.GetTicks(layout, plot).Count;

        renderer.AddSection(new AxisSection { From = 4, To = 6 });

        Assert.Equal(before, renderer.GetTicks(layout, plot).Count);
    }

    [Fact]
    public void An_axis_section_is_drawn_in_the_gutter_and_the_labels_stay_on_top_of_it()
    {
        var size = new Size(400, 200);
        var chart = Laid(Labelled());
        var labels = Ink.Count(Drawn(chart.HorizontalLayer), Colors.Black, 40);

        chart.HorizontalRenderer.AddSection(new AxisSection
        {
            From = 2,
            To = 4,
            Fill = new SolidColorBrush(Slab),
            Border = new Pen(new SolidColorBrush(Slab), 1),
        });

        var picture = Drawn(chart.HorizontalLayer);
        var slab = Ink.Bounds(picture, Slab, new Rect(0, 0, size.Width, size.Height), 20);
        var (low, high) = Ends(chart.HorizontalAxis, chart.Transform.GetLayout(ChartDimension.X), chart.PlotBounds, 2, 4);

        Assert.True(Ink.Count(picture, Slab, 20) > 0, "the slab was not drawn");
        Assert.True(slab.Top >= chart.PlotBounds.Bottom - 1, $"the slab is drawn over the plot, at {slab.Top}");
        Near(low, slab.Left);
        Near(high, slab.Right);

        // The labels are drawn over the slab rather than under it: a section behind them tells a reader what the
        // stretch means, and one in front of them hides the readings.
        Assert.True(
            Ink.Count(picture, Colors.Black, 40) > labels / 2,
            $"{Ink.Count(picture, Colors.Black, 40)} of {labels} label pixels are still on top of the slab");
    }

    [Fact]
    public void A_section_says_so_when_it_changes()
    {
        // A section is the axis' own setting like any other: the chart is told, because what the axis says changed.
        var renderer = new AxisRenderer(new LinearAxis(), ChartDimension.X);
        var section = new AxisSection { From = 1, To = 2, Name = "quiet" };
        var said = 0;

        renderer.Changed += (_, _) => said++;

        renderer.AddSection(section);
        section.To = 3;
        renderer.RemoveSection(section);

        Assert.Equal(3, said);
    }

    /// <summary>A chart with a window on it and nothing else, so that ink is the section's own.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart() => new()
    {
        HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
    };

    /// <summary>A chart whose horizontal axis carries a row of labels for a section to be written over.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Labelled()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis
            {
                ScaleMode = AxisScaleMode.Fixed,
                Minimum = 0,
                PixelsPerUnit = 40,
                Interval = 1,
            },
            VerticalAxis = new LinearAxis
            {
                ScaleMode = AxisScaleMode.Fixed,
                Minimum = 0,
                PixelsPerUnit = 40,
                Interval = 1,
            },
        };

        chart.HorizontalLayer.LabelBrush = Brushes.Black;
        chart.HorizontalLayer.AxisLinePen = new Pen(Brushes.Gray, 1);
        chart.HorizontalLayer.ShowGridLines = false;

        return chart;
    }

    /// <summary>Lays a chart out at the size the pictures are drawn at, which is where positions come from.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The same chart.</returns>
    private static CartesianChart Laid(CartesianChart chart)
    {
        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));

        return chart;
    }

    /// <summary>Draws a laid-out chart's annotation layer on its own, so the ink is the section's and nobody else's.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            chart.AnnotationLayer.Render(context);
        }

        return bitmap;
    }

    /// <summary>Draws an axis layer on its own.</summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Drawn(AxisLayer layer)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            layer.Render(context);
        }

        return bitmap;
    }

    /// <summary>Where a reading sits up a laid-out chart.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="value">The reading.</param>
    /// <returns>The position, in pixels.</returns>
    private static double Up(CartesianChart chart, double value) =>
        At(chart, ChartDimension.Y, value);

    /// <summary>Where a reading sits across a laid-out chart.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="value">The reading.</param>
    /// <returns>The position, in pixels.</returns>
    private static double Across(CartesianChart chart, double value) =>
        At(chart, ChartDimension.X, value);

    private static double At(CartesianChart chart, ChartDimension dimension, double value)
    {
        Assert.True(
            chart.Transform.TryValueToPosition(dimension, ChartValue.FromNumber(value), out var at),
            $"{value} has no position on {dimension}");

        return at;
    }

    /// <summary>Where two readings sit on an axis that was resolved without a chart, across it.</summary>
    /// <param name="axis">The axis.</param>
    /// <param name="layout">What the axis worked out.</param>
    /// <param name="plot">The plot.</param>
    /// <param name="first">One reading.</param>
    /// <param name="second">The other.</param>
    /// <returns>The two positions.</returns>
    private static (double Low, double High) Ends(
        IAxis axis,
        AxisLayout layout,
        Rect plot,
        double first,
        double second) =>
        (At(axis, layout, plot, first), At(axis, layout, plot, second));

    private static double At(IAxis axis, AxisLayout layout, Rect plot, double value)
    {
        Assert.True(axis.TryToUnit(ChartValue.FromNumber(value), out var unit));

        return plot.X + (layout.UnitRange.Normalize(unit) * plot.Width);
    }

    /// <summary>Says that a position and a pixel agree, within the pixel an antialiased edge rounds to.</summary>
    /// <param name="expected">Where it should be.</param>
    /// <param name="actual">Where it was found.</param>
    private static void Near(double expected, double actual) =>
        Assert.InRange(actual, expected - 1.5, expected + 1.5);
}
