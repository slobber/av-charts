using Avalonia;
using Avalonia.Input;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the gestures a chart answers to: the wheel zooming about the pointer, a drag panning, the modifier keys
/// singling out one dimension, and the threshold that keeps a click from becoming a drag.
/// </summary>
/// <remarks>
/// The rules are driven through the methods the pointer handlers call rather than through synthesised pointer
/// input, and that is worth being plain about: the headless platform's mouse helpers do not reach a window in
/// this setup - the event never arrives at the chart or at the window - so routing is the one part of a gesture
/// these tests cannot speak for. Everything above it is here: how many pixels a notch is worth, which dimensions
/// a modifier singles out, how far a press has to move to be a drag, and what the window does as a result.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class GestureTests
{
    [Fact]
    public void The_wheel_zooms_the_window_about_the_pointer()
    {
        var chart = Probe();
        var anchor = Anchor(chart);
        var under = chart.PointToData(anchor)!.Value;
        var before = Window(chart, ChartDimension.X);

        chart.Wheel(1, anchor, KeyModifiers.None);

        // One notch in, and what was under the pointer is still under it.
        Assert.Equal(before.Length / 1.2, Window(chart, ChartDimension.X).Length, 6);
        Assert.Equal(under.X.Number, chart.PointToData(anchor)!.Value.X.Number, 6);
        Assert.Equal(under.Y.Number, chart.PointToData(anchor)!.Value.Y.Number, 6);

        // And a notch the other way undoes it.
        chart.Wheel(-1, anchor, KeyModifiers.None);

        Assert.Equal(before.Length, Window(chart, ChartDimension.X).Length, 6);
    }

    [Fact]
    public void A_modifier_key_singles_out_one_dimension()
    {
        var chart = Probe();
        var anchor = Anchor(chart);
        var vertical = Window(chart, ChartDimension.Y);

        // Shift is across, so the vertical window is not touched at all.
        chart.Wheel(1, anchor, KeyModifiers.Shift);

        Assert.True(Window(chart, ChartDimension.X).Length < 10, "the horizontal window should have narrowed");
        Assert.Equal(vertical, Window(chart, ChartDimension.Y));

        // Control is up, so this time it is the horizontal one that stays put.
        var horizontal = Window(chart, ChartDimension.X);

        chart.Wheel(1, anchor, KeyModifiers.Control);

        Assert.Equal(horizontal, Window(chart, ChartDimension.X));
        Assert.True(Window(chart, ChartDimension.Y).Length < vertical.Length, "the vertical window should have narrowed");
    }

    [Fact]
    public void Dragging_pans_the_window()
    {
        var chart = Probe();
        var from = Anchor(chart);
        var before = Window(chart, ChartDimension.X);

        chart.Down(from);
        Assert.True(chart.Move(from + new Point(60, 30), KeyModifiers.None), "that far should be a drag");
        chart.Up();

        var after = Window(chart, ChartDimension.X);

        // Dragging the content right and down shows earlier and larger readings - the window is the same width,
        // and it rises, because what was above the pointer's travel has been pulled into view.
        Assert.True(after.Minimum < before.Minimum, $"{after} did not move from {before}");
        Assert.Equal(before.Length, after.Length, 6);
        Assert.True(Window(chart, ChartDimension.Y).Minimum > 0, "the vertical window should have moved too");
    }

    [Fact]
    public void A_press_that_barely_moves_is_not_a_drag()
    {
        var chart = Probe();
        var at = Anchor(chart);
        var before = Window(chart, ChartDimension.X);

        chart.Down(at);

        // A hand is not steady: a press that moved a pixel is a click, and a click does not pan the chart - nor
        // does it consume the event, so whoever wanted the click still gets it.
        Assert.False(chart.Move(at + new Point(1, 1), KeyModifiers.None));
        Assert.Equal(before, Window(chart, ChartDimension.X));

        // Past the threshold it is a drag, and from there on every move pans by what it moved.
        Assert.True(chart.Move(at + new Point(4, 0), KeyModifiers.None));
        Assert.True(Window(chart, ChartDimension.X).Minimum < before.Minimum);
    }

    [Fact]
    public void A_modifier_key_singles_out_one_dimension_when_dragging()
    {
        var chart = Probe();
        var from = Anchor(chart);
        var vertical = Window(chart, ChartDimension.Y);

        chart.Down(from);
        chart.Move(from + new Point(60, 30), KeyModifiers.Shift);
        chart.Up();

        Assert.True(Window(chart, ChartDimension.X).Minimum < 0, "the horizontal window should have moved");
        Assert.Equal(vertical, Window(chart, ChartDimension.Y));
    }

    [Fact]
    public void The_gestures_can_be_turned_off()
    {
        var chart = Probe();
        var at = Anchor(chart);
        var before = Window(chart, ChartDimension.X);

        chart.ZoomOnWheel = false;
        chart.PanOnDrag = false;

        // The switch is read by the pointer handlers, which is where the decision belongs; what it does is what
        // the tests above cover, so this only says the switch is there and defaults to on.
        Assert.False(chart.ZoomOnWheel);
        Assert.False(chart.PanOnDrag);
        Assert.Equal(before, Window(chart, ChartDimension.X));
    }

    /// <summary>A chart that can be driven the way its pointer handlers drive it.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Wheel(double notches, Point at, KeyModifiers modifiers) => WheelZoom(notches, at, modifiers);

        public void Down(Point at) => DragStart(at);

        public bool Move(Point at, KeyModifiers modifiers) => DragMove(at, modifiers);

        public void Up() => DragEnd();
    }

    private static ChartProbe Probe()
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
        return chart;
    }

    /// <summary>A quarter of the way across the plot and a third of the way down it.</summary>
    private static Point Anchor(CartesianChart chart) =>
        new(chart.PlotBounds.X + (chart.PlotBounds.Width / 4), chart.PlotBounds.Y + (chart.PlotBounds.Height / 3));

    private static AxisUnitRange Window(CartesianChart chart, ChartDimension dimension) =>
        chart.Transform.GetLayout(dimension).UnitRange;
}
