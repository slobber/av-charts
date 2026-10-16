using Avalonia;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the window a chart shows: moving it, zooming it about a point, asking where a value is and what is
/// under a point, and being told when it changed.
/// </summary>
/// <remarks>
/// The window is the axes' own business, so what is asserted is what the axes now say - in unit space, through
/// the transform, which is where a caller reads the visible range from.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ViewportTests
{
    [Fact]
    public void Panning_moves_the_window_without_changing_its_length()
    {
        // An explicit interval, because a fixed scale rounds its anchor onto the grid of whatever interval it
        // would otherwise choose - which is right, and would make this test about the ladder rather than about
        // panning.
        var chart = Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 10, Minimum = 0, Interval = 1 });
        var before = Window(chart, ChartDimension.X);

        chart.PanBy(60, 0);
        Arrange(chart);

        var after = Window(chart, ChartDimension.X);

        // Sixty pixels at ten pixels a unit is six units, and dragging the content right shows earlier data.
        Assert.Equal(before.Minimum - 6, after.Minimum, 3);
        Assert.Equal(before.Length, after.Length, 3);
        Assert.Equal(10, Axis(chart, ChartDimension.X).PixelsPerUnit, 3);
    }

    [Fact]
    public void Panning_a_fitting_scale_moves_both_ends()
    {
        var chart = Chart(new LinearAxis { Minimum = 0, Maximum = 10 });
        var before = Window(chart, ChartDimension.X);

        chart.PanBy(0, 30);
        Arrange(chart);

        var after = Window(chart, ChartDimension.X);

        Assert.Equal(before.Minimum, after.Minimum, 3);
        Assert.Equal(before.Maximum, after.Maximum, 3);

        // Vertically it moves too, and the same way round as the paper under the hand: dragging the content down
        // shows the readings that were above it, so the window's floor rises.
        var vertical = Window(chart, ChartDimension.Y);

        Assert.True(vertical.Minimum > 0, $"the vertical window should have moved to {vertical}");
        Assert.Equal(10, vertical.Length, 3);
    }

    [Fact]
    public void A_pan_moves_the_ink_the_way_the_hand_did()
    {
        // What a reader does with a drag is take hold of the picture, and a picture that goes the other way from
        // the hand reads as broken however right the arithmetic is. So this asserts the thing the hand sees: the
        // same reading, the same distance away as the pointer was dragged - on both dimensions, which is where
        // the sign of a screen's y against a value's y has to come out.
        var chart = Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 10, Minimum = 0, Interval = 1 });

        // No labels, so that the gutter - and with it the plot rectangle the mapping is taken in - cannot move
        // under the assertion.
        chart.VerticalRenderer.ShowLabels = false;
        chart.HorizontalRenderer.ShowLabels = false;
        Arrange(chart);

        var where = chart.DataToPoint(5, 5);

        Assert.NotNull(where);

        chart.PanBy(40, 30);
        Arrange(chart);

        var moved = chart.DataToPoint(5, 5);

        Assert.NotNull(moved);
        Assert.Equal(where!.Value.X + 40, moved!.Value.X, 1);
        Assert.Equal(where.Value.Y + 30, moved.Value.Y, 1);
    }

    [Fact]
    public void Zooming_a_fitting_window_keeps_the_value_under_the_pointer_exactly()
    {
        var chart = Chart(new LinearAxis { Minimum = 0, Maximum = 10 });
        var plot = chart.PlotBounds;
        var anchor = new Point(plot.X + (plot.Width / 4), plot.Y + (plot.Height / 3));
        var under = chart.PointToData(anchor)!.Value;
        var before = Window(chart, ChartDimension.X);

        chart.ZoomBy(2, anchor);

        var after = chart.PointToData(anchor)!.Value;
        var window = Window(chart, ChartDimension.X);

        // Fitting the data, the window is written freely, so what was under the pointer stays exactly under it.
        // The layout is left where it is on purpose: arranging again would let a wider label move the plot, and
        // then the pointer would be over a different fraction of it - a fact about labels living outside the plot
        // rather than about zooming.
        Assert.Equal(under.X.Number, after.X.Number, 6);
        Assert.Equal(under.Y.Number, after.Y.Number, 6);
        Assert.Equal(before.Length / 2, window.Length, 6);
    }

    [Fact]
    public void Zooming_a_fixed_scale_scales_the_pixels_and_lands_the_anchor_on_the_grid()
    {
        var chart = Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 10, Minimum = 0, Interval = 1 });
        var plot = chart.PlotBounds;
        var anchor = new Point(plot.X + (plot.Width / 4), plot.Y + (plot.Height / 3));
        var under = chart.PointToData(anchor)!.Value;
        var before = Window(chart, ChartDimension.X);

        chart.ZoomBy(2, anchor);

        var after = chart.PointToData(anchor)!.Value;
        var window = Window(chart, ChartDimension.X);

        // Zooming a fixed scale is changing how many pixels a unit is worth, which is what the brief asks for, and
        // the window is half as many units wide as a result.
        Assert.Equal(20, Axis(chart, ChartDimension.X).PixelsPerUnit, 3);
        Assert.Equal(before.Length / 2, window.Length, 3);

        // What was under the pointer stays there to within a grid line: a fixed scale keeps its anchor on the
        // grid, which is the rule that makes a zoom land on readable values rather than on 12.437.
        var interval = chart.Transform.GetLayout(ChartDimension.X).MajorInterval;

        Assert.True(
            Math.Abs(after.X.Number - under.X.Number) <= interval,
            $"{under.X.Number} became {after.X.Number}, more than one grid line of {interval} away");
    }

    [Fact]
    public void Zooming_a_fixed_scale_changes_pixels_per_unit()
    {
        var chart = Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 10, Minimum = 0 });

        chart.ZoomBy(2, chart.PlotBounds.Center);
        Arrange(chart);

        Assert.Equal(20, Axis(chart, ChartDimension.X).PixelsPerUnit, 3);
        Assert.Equal(20, Axis(chart, ChartDimension.Y).PixelsPerUnit, 3);
    }

    [Fact]
    public void A_point_and_a_value_are_two_ways_of_saying_the_same_thing()
    {
        var chart = Chart(new LinearAxis { Minimum = 0, Maximum = 10 });

        var point = chart.DataToPoint(2.5, 7.5);

        Assert.NotNull(point);

        var back = chart.PointToData(point.Value)!.Value;

        Assert.Equal(2.5, back.X.Number, 3);
        Assert.Equal(7.5, back.Y.Number, 3);

        // A chart with nothing to place says so rather than inventing a position.
        Assert.Null(chart.DataToPoint(ChartValue.Empty, 1));
    }

    [Fact]
    public void The_chart_says_when_its_window_moves()
    {
        var chart = Chart(new LinearAxis { Minimum = 0, Maximum = 10 });
        var told = new List<VisibleRangeChangedEventArgs>();

        chart.VisibleRangeChanged += (_, arguments) => told.Add(arguments);

        chart.PanBy(60, 30);
        Arrange(chart);

        // One announcement per dimension, each with what that dimension now shows.
        Assert.Equal(2, told.Count);

        var horizontal = told.Single(arguments => arguments.Dimension == ChartDimension.X);

        Assert.Equal(Window(chart, ChartDimension.X), horizontal.Range);
        Assert.True(horizontal.Range.Minimum < 0, "the horizontal window should have moved");

        // And nothing is said when nothing moved.
        told.Clear();
        Arrange(chart);

        Assert.Empty(told);
    }

    [Fact]
    public void A_move_of_nothing_moves_nothing()
    {
        var chart = Chart(new LinearAxis { Minimum = 0, Maximum = 10 });
        var told = 0;

        chart.VisibleRangeChanged += (_, _) => told++;

        chart.PanBy(0, 0);
        chart.ZoomBy(0, chart.PlotBounds.Center);
        chart.ZoomBy(double.NaN, chart.PlotBounds.Center);
        chart.ZoomBy(-1, chart.PlotBounds.Center);
        Arrange(chart);

        Assert.Equal(0, told);

        var window = Window(chart, ChartDimension.X);

        Assert.Equal(0, window.Minimum, 3);
        Assert.Equal(10, window.Maximum, 3);
    }

    private static AxisBase Axis(CartesianChart chart, ChartDimension dimension) =>
        Assert.IsAssignableFrom<AxisBase>(chart.Transform.GetAxis(dimension));

    private static AxisUnitRange Window(CartesianChart chart, ChartDimension dimension) =>
        chart.Transform.GetLayout(dimension).UnitRange;

    private static void Arrange(CartesianChart chart)
    {
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
    }

    private static CartesianChart Chart(IAxis axis)
    {
        var chart = new CartesianChart { HorizontalAxis = axis, VerticalAxis = Twin(axis) };

        Arrange(chart);
        return chart;
    }

    /// <summary>The same settings on a second axis, since one axis serves one dimension.</summary>
    private static LinearAxis Twin(IAxis axis) =>
        axis is AxisBase settings
            ? new LinearAxis
            {
                ScaleMode = settings.ScaleMode,
                PixelsPerUnit = settings.PixelsPerUnit,
                Minimum = settings.Minimum,
                Maximum = settings.Maximum,
            }
            : new LinearAxis();
}
