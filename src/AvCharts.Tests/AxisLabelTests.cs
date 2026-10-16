using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// The measuring of a label and the drawing of it have to agree, or a gutter that was worked out from the
/// measurement will be too small and the label will be cut off at the edge of the control.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class AxisLabelTests
{
    [Fact]
    public void A_tick_reads_as_a_number_rather_than_as_arithmetic()
    {
        var axis = new LinearAxis();

        // A step of 0.2 added three times is 0.6000000000000001 as a double: a grid line labelled with that is
        // unreadable, and because the gutter is measured from the widest label it is also expensive.
        Assert.Equal("0.6", axis.FormatValue(0.6000000000000001));
        Assert.Equal("0.25", axis.FormatValue(0.25));
        Assert.Equal("60", axis.FormatValue(60));
    }

    [Fact]
    public void The_gutter_the_plot_leaves_is_the_gutter_the_labels_asked_for()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 2 },
        };

        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));

        var vertical = chart.Transform.GetLayout(ChartDimension.Y);
        var gutter = chart.VerticalRenderer.MeasureGutter(vertical);

        Assert.True(
            vertical.MajorTicks.Count > 0,
            $"the vertical axis has no ticks at all (range {vertical.UnitRange}, interval {vertical.MajorInterval})");
        Assert.True(
            chart.PlotBounds.Left >= gutter,
            $"the plot starts at {chart.PlotBounds.Left} but the labels asked for {gutter} " +
            $"(widest of {vertical.MajorTicks.Count} labels)");
    }

    [Fact]
    public void A_label_is_drawn_no_wider_than_it_was_measured()
    {
        var renderer = new AxisRenderer(new LinearAxis(), ChartDimension.Y) { FontSize = 11 };
        var label = renderer.CreateLabel("10", Brushes.Black);

        var bitmap = new RenderTargetBitmap(new PixelSize(200, 60));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawText(label, new Point(20, 10));
        }

        var (left, _, right, _) = Ink.Bounds(bitmap);
        var drawn = (right - left) + 1;

        Assert.True(drawn > 0, "the label was not drawn at all");
        Assert.True(
            drawn <= Math.Ceiling(label.Width) + 2,
            $"measured {label.Width} but drew {drawn}: the gutter cannot trust the measurement");
        Assert.True(left >= 18, $"the label started at {left}, left of where it was placed");
    }

    [Fact]
    public void A_label_that_is_turned_is_drawn_inside_the_room_it_asked_for()
    {
        // What the axis measured is the box a label needs once it is turned, and the layer has to place the label
        // by that box - so the two halves can be held against each other: the plot gives up the room the gutter
        // grew by, and what is drawn stays inside the room that is left. A layer that placed a turned label by its
        // upright width would hang half of it outside the control while the gutter it asked for looked generous.
        var size = new Size(400, 200);
        var chart = Labelled();

        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var upright = chart.PlotBounds.Height;
        var renderer = chart.HorizontalRenderer;

        renderer.LabelRotation = 60;

        // Laid out again, which is what the library's own invalidation asks an application for: the gutter grew, so
        // the plot has less room than it had a moment ago.
        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var asked = renderer.MeasureGutter(chart.Transform.GetLayout(ChartDimension.X));
        var edge = chart.PlotBounds.Bottom;
        var bitmap = Drawn(chart);
        var (_, top, _, bottom) = Ink.Bounds(bitmap);

        Assert.True(Ink.Count(bitmap) > 0, "the axis drew nothing at all");
        Assert.True(
            chart.PlotBounds.Height < upright,
            $"the plot is {chart.PlotBounds.Height} tall with the labels turned, and was {upright} upright");
        // Two pixels of slack for the axis line itself, which is a stroke: half of its width falls on the plot's
        // side of the edge it is drawn along, and the edge is at a fraction of a pixel.
        Assert.True(top >= edge - 2, $"the turned labels are drawn {edge - top} px over the plot");
        Assert.True(
            bottom <= edge + asked + 1,
            $"the labels reach {bottom - edge} px below the plot, and the axis asked for {asked}");
        Assert.True(bottom < 200, "the turned labels run off the bottom of the picture");
    }

    [Fact]
    public void Staggered_labels_are_drawn_in_two_rows()
    {
        // A gutter that was doubled to pay for a second row of labels has to get one, and it is the odd labels that
        // stand in it: the whole axis moves up by exactly the one row the plot gave back, the labels reach no
        // deeper than they did, and the band one row further out is empty with one row of labels and carries ink
        // with two.
        var size = new Size(400, 200);
        var chart = Labelled();

        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var renderer = chart.HorizontalRenderer;
        var row = renderer.MeasureLabel("10").Height;
        var beyond = renderer.TickLength + renderer.LabelGap;
        var one = Drawn(chart);
        var (_, oneTop, _, oneBottom) = Ink.Bounds(one);
        var oneSecondRow = new Rect(0, chart.PlotBounds.Bottom + beyond + row, 400, row);

        renderer.StaggerLabels = true;
        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var two = Drawn(chart);
        var (_, twoTop, _, twoBottom) = Ink.Bounds(two);
        var twoSecondRow = new Rect(0, chart.PlotBounds.Bottom + beyond + row, 400, row);

        Assert.InRange(oneTop - twoTop, row - 1.5, row + 1.5);
        Assert.Equal((double)oneBottom, twoBottom, 1);
        Assert.Equal(0, Ink.Count(one, oneSecondRow));
        Assert.True(Ink.Count(two, twoSecondRow) > 0, "no label is drawn in the second row");
    }

    /// <summary>
    /// A chart whose horizontal axis carries more labels than its room can hold: a fixed scale of forty pixels to a
    /// unit with a label every half a unit, so that the labels are the axis' own doing rather than a series' data.
    /// </summary>
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
                Interval = 0.5,
            },
            VerticalAxis = new LinearAxis
            {
                ScaleMode = AxisScaleMode.Fixed,
                Minimum = 0,
                PixelsPerUnit = 40,
                Interval = 1,
            },
        };

        // A chart in a tree takes its inks from the theme; a chart on its own is told them, which is what a host
        // without a theme does and what a chart painted on its own has to be given here.
        chart.HorizontalLayer.LabelBrush = Brushes.Black;
        chart.HorizontalLayer.AxisLinePen = new Pen(Brushes.Gray, 1);
        chart.HorizontalLayer.ShowGridLines = false;

        return chart;
    }

    /// <summary>Draws a chart's horizontal axis on its own, so that a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Drawn(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            chart.HorizontalLayer.Render(context);
        }

        return bitmap;
    }
}
