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
        var bitmap = Drawn(chart.HorizontalLayer);
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
        var one = Drawn(chart.HorizontalLayer);
        var (_, oneTop, _, oneBottom) = Ink.Bounds(one);
        var oneSecondRow = new Rect(0, chart.PlotBounds.Bottom + beyond + row, 400, row);

        renderer.StaggerLabels = true;
        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var two = Drawn(chart.HorizontalLayer);
        var (_, twoTop, _, twoBottom) = Ink.Bounds(two);
        var twoSecondRow = new Rect(0, chart.PlotBounds.Bottom + beyond + row, 400, row);

        Assert.InRange(oneTop - twoTop, row - 1.5, row + 1.5);
        Assert.Equal((double)oneBottom, twoBottom, 1);
        Assert.Equal(0, Ink.Count(one, oneSecondRow));
        Assert.True(Ink.Count(two, twoSecondRow) > 0, "no label is drawn in the second row");
    }

    [Fact]
    public void A_title_is_drawn_beyond_the_labels_and_inside_the_room_it_asked_for()
    {
        // The title is the second piece of writing out from the plot: it must not land among the labels - read there
        // it would be taken for one of them - and it must land inside the room the axis measured for it, or it is cut
        // off by the edge of the control.
        var size = new Size(400, 200);
        var chart = Titled();

        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var bitmap = DrawnOnWhite(chart.HorizontalLayer);
        var whole = new Rect(0, 0, size.Width, size.Height);
        var labels = Ink.Bounds(bitmap, Colors.Black, whole);
        var title = Ink.Bounds(bitmap, Colors.Red, whole);
        var edge = chart.PlotBounds.Bottom;
        var asked = chart.HorizontalRenderer.MeasureGutter(chart.Transform.GetLayout(ChartDimension.X));

        Assert.True(Ink.Count(bitmap) > 0, "the axis drew nothing at all");
        Assert.True(title.Bottom > title.Top, "the title was not drawn");
        Assert.True(
            title.Top > labels.Bottom,
            $"the title starts at {title.Top} and the labels end at {labels.Bottom}");
        Assert.True(
            title.Bottom <= edge + asked + 1,
            $"the title reaches {title.Bottom - edge} px below the plot, and the axis asked for {asked}");
        Assert.True(title.Bottom < size.Height, "the title runs off the bottom of the control");
    }

    [Fact]
    public void A_title_up_a_vertical_axis_is_drawn_turned_and_beyond_the_labels()
    {
        var size = new Size(400, 200);
        var chart = Titled();

        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var bitmap = DrawnOnWhite(chart.VerticalLayer);
        var whole = new Rect(0, 0, size.Width, size.Height);
        var labels = Ink.Bounds(bitmap, Colors.Black, whole);
        var title = Ink.Bounds(bitmap, Colors.Red, whole);

        Assert.True(Ink.Count(bitmap) > 0, "the axis drew nothing at all");
        Assert.True(title.Bottom > title.Top, "the title was not drawn");
        Assert.True(
            title.Right < labels.Left,
            $"the title ends at {title.Right} and the labels start at {labels.Left}");
        Assert.True(title.Left >= 0, "the title runs off the left of the control");
        Assert.True(title.Bottom <= size.Height, "the title runs off the bottom of the control");
    }

    [Fact]
    public void A_vertical_title_reads_the_way_it_is_asked_to()
    {
        // The two directions are the two ways of turning the same piece of writing, so it stands in the same place
        // and takes the same room either way - and the picture is not the same one.
        var size = new Size(400, 200);
        var chart = Titled();

        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var whole = new Rect(0, 0, size.Width, size.Height);
        var up = DrawnOnWhite(chart.VerticalLayer);
        var upTitle = Ink.Bounds(up, Colors.Red, whole);

        chart.VerticalRenderer.TitleDirection = AxisTitleDirection.TopToBottom;
        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var down = DrawnOnWhite(chart.VerticalLayer);
        var downTitle = Ink.Bounds(down, Colors.Red, whole);

        Assert.True(Ink.Differences(up, down) > 0, "the title reads the same either way");

        // The same room, to the pixel the two turnings round to.
        Assert.InRange(downTitle.Left, upTitle.Left - 2, upTitle.Left + 2);
        Assert.InRange(downTitle.Top, upTitle.Top - 2, upTitle.Top + 2);
        Assert.InRange(downTitle.Right, upTitle.Right - 2, upTitle.Right + 2);
        Assert.InRange(downTitle.Bottom, upTitle.Bottom - 2, upTitle.Bottom + 2);
    }

    /// <summary>A chart whose two axes are both named, so that either of them has a title to draw.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Titled()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis
            {
                ScaleMode = AxisScaleMode.Fixed,
                Minimum = 0,
                PixelsPerUnit = 40,
                Interval = 1,
                Title = "Seconds",
            },
            VerticalAxis = new LinearAxis
            {
                ScaleMode = AxisScaleMode.Fixed,
                Minimum = 0,
                PixelsPerUnit = 40,
                Interval = 1,
                Title = "Headcount",
            },
        };

        // Two brushes that can be told apart in the picture, so that a claim about the labels and a claim about the
        // title can be made about the same drawing.
        foreach (var layer in new[] { chart.HorizontalLayer, chart.VerticalLayer })
        {
            layer.LabelBrush = Brushes.Black;
            layer.TitleBrush = Brushes.Red;
            layer.AxisLinePen = new Pen(Brushes.Gray, 1);
            layer.ShowGridLines = false;
        }

        return chart;
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

    /// <summary>Draws an axis on a sheet of white, so that a claim about one colour can be made about one thing.</summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The picture.</returns>
    /// <remarks>
    /// Because text drawn straight onto an empty bitmap is fringed with black pixels: Skia renders it a subpixel at
    /// a time, so a red glyph on nothing at all leaves pixels whose red channel is zero with their alpha full - and
    /// a test looking for the black writing finds the red writing's edges. On paper the fringe is a blend with the
    /// paper, which is what a chart is drawn on.
    /// </remarks>
    private static RenderTargetBitmap DrawnOnWhite(AxisLayer layer)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, 400, 200));
            layer.Render(context);
        }

        return bitmap;
    }
}
