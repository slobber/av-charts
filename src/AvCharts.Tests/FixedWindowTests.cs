using Avalonia;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// A fixed scale is the one mode where the plot's size decides how much data is on screen, and where the window
/// moves by being told to rather than by fitting itself to the data. These are the semantics the design brief
/// settled on, pinned at the level a caller actually works at.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class FixedWindowTests
{
    [Fact]
    public void A_fixed_scale_pans_by_what_was_dragged_however_short()
    {
        // A strip chart is read by dragging it: the window moves by the pixels the pointer moved, and the grid
        // slides with the readings. Rounding the window's start onto the interval instead - which is what this
        // did at first - meant a short drag moved nothing at all and a drag the other way jumped a whole cell.
        var chart = Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 40, Minimum = 29, Interval = 1 });
        var length = chart.Transform.GetLayout(ChartDimension.X).UnitRange.Length;

        chart.PanBy(8, 0);      // a fifth of a second towards earlier readings
        Assert.Equal(28.8, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Minimum, 6);

        chart.PanBy(30, 0);     // and three quarters of a second further
        Assert.Equal(28.05, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Minimum, 6);

        chart.PanBy(-38, 0);    // and back, by exactly what was dragged
        Assert.Equal(29, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Minimum, 6);

        // The window keeps its length, which is the whole point of a fixed scale.
        Assert.Equal(length, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Length, 6);
    }
    private static CartesianChart Chart(LinearAxis horizontal, double width = 200)
    {
        var chart = new CartesianChart { HorizontalAxis = horizontal };

        chart.VerticalRenderer.ShowLabels = false;
        chart.HorizontalRenderer.ShowLabels = false;

        chart.Measure(new Size(width, 100));
        chart.Arrange(new Rect(0, 0, width, 100));

        return chart;
    }

    private static double Span(CartesianChart chart) =>
        chart.Transform.GetLayout(ChartDimension.X).UnitRange.Length;

    [Fact]
    public void A_wider_plot_shows_more_data_rather_than_wider_spacing()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 0 };

        var narrow = Span(Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 0 }, 200));
        var wide = Span(Chart(new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 0 }, 400));

        Assert.True(wide > narrow * 1.8, $"a plot twice as wide showed {wide} against {narrow}");
        Assert.NotSame(axis, null);
    }

    [Fact]
    public void Zooming_is_changing_how_many_pixels_a_unit_takes()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 0 };
        var chart = Chart(axis);

        axis.PixelsPerUnit = 8;

        // Four times the pixels per unit is a quarter of the data on screen, for the same plot.
        Assert.Equal(chart.PlotBounds.Width / 8, Span(chart), 6);
    }

    [Fact]
    public void Panning_is_moving_the_anchor_and_leaves_the_span_alone()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 0, Interval = 5 };
        var chart = Chart(axis);

        axis.Minimum = 20;

        Assert.Equal(chart.PlotBounds.Width / 2, Span(chart), 6);
        Assert.Equal(20, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Minimum, 6);
    }

    [Fact]
    public void The_window_starts_where_it_was_told_to_and_the_grid_is_still_on_the_interval()
    {
        // An awkward anchor is taken as it stands: a fixed scale is dragged by the pixel, so its window cannot be
        // rounded onto the grid without making short drags do nothing at all - and the lines stay where the
        // interval says they are, in the window rather than at the window's start.
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 4, Minimum = 12, Interval = 5 };

        var layout = Chart(axis).Transform.GetLayout(ChartDimension.X);

        Assert.Equal(12, layout.UnitRange.Minimum, 6);
        Assert.Equal(15, layout.MajorTicks[0], 6);
        Assert.All(layout.MajorTicks, tick => Assert.Equal(0, tick % 5, 6));
    }

    [Fact]
    public void The_last_column_may_be_partly_cut_off_and_the_grid_still_reaches_the_edge()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 3, Minimum = 0, Interval = 7 };

        var layout = Chart(axis, 200).Transform.GetLayout(ChartDimension.X);
        var remainder = layout.UnitRange.Maximum - layout.MajorTicks[^1];

        // The grid is drawn to the end of the window: the last line is less than one interval from the edge,
        // and there is nothing beyond it to draw.
        Assert.True(remainder >= 0, $"the last grid line at {layout.MajorTicks[^1]} is past the window end {layout.UnitRange.Maximum}");
        Assert.True(remainder < layout.MajorInterval, $"{remainder} of {layout.MajorInterval} left over");
    }

    [Fact]
    public void The_grid_does_not_get_denser_when_the_chart_is_widened()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 0, MajorGridPitch = 60 };

        var narrow = Chart(axis, 200).Transform.GetLayout(ChartDimension.X);
        var wide = Chart(axis, 600).Transform.GetLayout(ChartDimension.X);

        // The scale is the same, so the gap between grid lines is the same; only the number of them grows.
        Assert.Equal(narrow.MajorInterval, wide.MajorInterval, 6);
        Assert.True(wide.MajorTicks.Count > narrow.MajorTicks.Count);
    }
}
