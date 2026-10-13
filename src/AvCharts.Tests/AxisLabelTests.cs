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
}
