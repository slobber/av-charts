using Avalonia;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

[Collection(AvaloniaCollection.Name)]
public class AxisRendererTests
{
    private static AxisLayout Layout(params double[] ticks) =>
        new(new AxisUnitRange(ticks[0], ticks[^1]), new EvenStep(ticks[1] - ticks[0]), 1, ticks, []);

    private static AxisRenderer Renderer(ChartDimension dimension) => new(new LinearAxis(), dimension);

    [Fact]
    public void A_vertical_axis_maps_its_values_from_the_bottom_up()
    {
        var renderer = Renderer(ChartDimension.Y);

        var ticks = renderer.GetTicks(Layout(0, 5, 10), new Rect(0, 0, 100, 100));

        Assert.Equal([100d, 50d, 0d], ticks.Select(tick => tick.Position));
    }

    [Fact]
    public void A_horizontal_axis_maps_its_values_from_the_left()
    {
        var renderer = Renderer(ChartDimension.X);

        var ticks = renderer.GetTicks(Layout(0, 5, 10), new Rect(0, 0, 100, 100));

        Assert.Equal([0d, 50d, 100d], ticks.Select(tick => tick.Position));
    }

    [Fact]
    public void The_labels_come_from_the_axis()
    {
        var renderer = Renderer(ChartDimension.X);

        var ticks = renderer.GetTicks(Layout(0, 5, 10), new Rect(0, 0, 100, 100));

        Assert.Equal(["0", "5", "10"], ticks.Select(tick => tick.Label));
    }

    [Fact]
    public void The_gutter_holds_the_widest_label()
    {
        var renderer = Renderer(ChartDimension.Y);
        renderer.TickLength = 4;
        renderer.LabelGap = 4;
        renderer.FontSize = 11;

        var gutter = renderer.MeasureGutter(Layout(0, 5, 10));

        // Two characters of text at 11 points: more than the marks and the gap, less than any generous bound.
        Assert.True(gutter > 8, $"expected more than the tick and the gap, got {gutter}");
        Assert.True(gutter < 8 + 22, $"expected less than two wide characters, got {gutter}");
    }

    [Fact]
    public void A_horizontal_axis_only_needs_the_height_of_a_label()
    {
        var renderer = Renderer(ChartDimension.X);
        renderer.FontSize = 11;

        var gutter = renderer.MeasureGutter(Layout(0, 5, 10));

        Assert.True(gutter > 8, $"expected more than the tick and the gap, got {gutter}");
        Assert.True(gutter < 8 + 20, $"expected a single line of text, got {gutter}");
    }

    [Fact]
    public void Labels_that_would_collide_are_dropped()
    {
        var renderer = Renderer(ChartDimension.Y);
        var ticks = Enumerable.Range(0, 21).Select(index => (double)index).ToArray();

        // Twenty-one labels into twenty pixels: almost all of them have to go.
        var kept = renderer.GetTicks(Layout(ticks), new Rect(0, 0, 100, 20));

        Assert.InRange(kept.Count, 1, 3);
        Assert.Equal(20, kept[0].Position);
    }

    [Fact]
    public void Labels_that_fit_are_all_kept()
    {
        var renderer = Renderer(ChartDimension.Y);
        var ticks = Enumerable.Range(0, 11).Select(index => index * 10d).ToArray();

        var kept = renderer.GetTicks(Layout(ticks), new Rect(0, 0, 100, 200));

        Assert.Equal(11, kept.Count);
        Assert.Equal(0, kept[^1].Position);
    }

    [Fact]
    public void Turning_the_labels_off_leaves_only_the_marks()
    {
        var renderer = Renderer(ChartDimension.Y);
        renderer.ShowLabels = false;
        renderer.TickLength = 4;

        Assert.Equal(4, renderer.MeasureGutter(Layout(0, 5, 10)));
        Assert.Equal(3, renderer.GetTicks(Layout(0, 5, 10), new Rect(0, 0, 100, 20)).Count);
    }

    [Fact]
    public void A_mark_drawn_into_the_plot_takes_no_room_from_the_margin()
    {
        // Flex's four tick placements, read for what they cost the margin: a mark outside the plot wants its whole
        // length, one across wants the half that leaves, and one drawn inside - or not drawn at all - wants none.
        var renderer = Renderer(ChartDimension.Y);
        renderer.TickLength = 6;

        var outside = renderer.MeasureGutter(Layout(0, 5, 10));

        renderer.TickPlacement = AxisTickPlacement.Cross;
        Assert.Equal(outside - 3, renderer.MeasureGutter(Layout(0, 5, 10)), 6);

        renderer.TickPlacement = AxisTickPlacement.Inside;
        Assert.Equal(outside - 6, renderer.MeasureGutter(Layout(0, 5, 10)), 6);

        renderer.TickPlacement = AxisTickPlacement.None;
        Assert.Equal(outside - 6, renderer.MeasureGutter(Layout(0, 5, 10)), 6);

        // And with the labels off, the marks are the whole of what an axis asks for.
        renderer.TickPlacement = AxisTickPlacement.Outside;
        renderer.ShowLabels = false;
        Assert.Equal(6, renderer.MeasureGutter(Layout(0, 5, 10)), 6);

        renderer.TickPlacement = AxisTickPlacement.Inside;
        Assert.Equal(0, renderer.MeasureGutter(Layout(0, 5, 10)), 6);
    }

    [Fact]
    public void An_axis_that_covers_nothing_has_no_ticks()
    {
        var renderer = Renderer(ChartDimension.X);

        Assert.Empty(renderer.GetTicks(AxisLayout.Empty, new Rect(0, 0, 100, 100)));
        Assert.Equal(4, Renderer(ChartDimension.X).MeasureGutter(AxisLayout.Empty));
    }
}
