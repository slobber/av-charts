using Avalonia;
using AvCharts.Axes;


using AvCharts.Data;


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

    [Fact]
    public void A_turned_label_costs_the_box_it_needs()
    {
        // A turned label is measured by the box it needs once it is turned rather than by the width and height it
        // has upright: the two each cast a shadow on both axes, and the box is what the shadows add up to. On a
        // vertical axis the labels stand out from the plot sideways, so what the gutter pays for is that box's
        // width.
        var renderer = Renderer(ChartDimension.Y);
        renderer.TickLength = 4;
        renderer.LabelGap = 4;
        renderer.LabelMargin = 4;

        var label = renderer.MeasureLabel("10");
        var upright = renderer.MeasureGutter(Layout(0, 5, 10));

        Assert.Equal(12 + label.Width, upright, 6);

        renderer.LabelRotation = 45;
        Assert.Equal(12 + ((label.Width + label.Height) / Math.Sqrt(2)), renderer.MeasureGutter(Layout(0, 5, 10)), 6);

        // A quarter turn stands a label on its end: what shows outside the plot is its height, and no more.
        renderer.LabelRotation = 90;
        Assert.Equal(12 + label.Height, renderer.MeasureGutter(Layout(0, 5, 10)), 6);
    }

    [Fact]
    public void Turning_the_labels_lets_more_of_them_fit()
    {
        // The reason to turn a label at all: a date written out is five times as wide as it is tall, and the same
        // date stood on its end takes a fifth of the room along the axis. Twenty-one of them on a hundred pixels
        // is a row that loses most of its labels upright and keeps them turned.
        var renderer = new AxisRenderer(new DateTimeAxis(), ChartDimension.X);
        var eightOfOctober = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var start = DateTimeAxis.ToMilliseconds(eightOfOctober);
        var layout = Layout([.. Enumerable.Range(0, 21).Select(index => start + (index * 86_400_000d))]);
        var plot = new Rect(0, 0, 100, 50);

        var upright = renderer.GetTicks(layout, plot);

        renderer.LabelRotation = 90;
        var turned = renderer.GetTicks(layout, plot);

        Assert.InRange(upright.Count, 1, 2);
        Assert.True(
            turned.Count > 2 * upright.Count,
            $"{upright.Count} of twenty-one dates fit upright and {turned.Count} turned");
        Assert.Equal(0d, turned[0].Position);
    }

    [Fact]
    public void Staggered_labels_keep_twice_as_many_in_the_same_room()
    {
        // Two rows and a rule that only compares a label with the one before it in its own row: the neighbour in
        // the other row may overlap it, and that overlap is the whole of what staggering buys.
        var renderer = Renderer(ChartDimension.X);
        renderer.LabelGap = 4;

        var layout = Layout([.. Enumerable.Range(0, 21).Select(index => index * 5d)]);
        var plot = new Rect(0, 0, 100, 50);
        var one = renderer.GetTicks(layout, plot);

        renderer.StaggerLabels = true;
        var two = renderer.GetTicks(layout, plot);

        Assert.True(
            two.Count >= (2 * one.Count) - 1,
            $"{one.Count} labels fit in one row and {two.Count} in two");

        // Which labels those are is the arithmetic read back. Two labels one place apart are in different rows and
        // may well overlap - that is what the second row is for. Two places apart they are in one row and may not.
        double Apart(AxisTick first, AxisTick second) => second.Position - first.Position;

        double Together(AxisTick first, AxisTick second) =>
            (renderer.MeasureLabel(first.Label).Width + renderer.MeasureLabel(second.Label).Width) / 2;

        var overlaps = 0;

        for (var index = 1; index < two.Count; index++)
        {
            if (Apart(two[index - 1], two[index]) < Together(two[index - 1], two[index]))
            {
                overlaps++;
            }
        }

        Assert.True(overlaps > 0, "no two labels overlapped, so the second row bought nothing");

        for (var index = 2; index < two.Count; index++)
        {
            Assert.True(
                Apart(two[index - 2], two[index]) > Together(two[index - 2], two[index]) + renderer.LabelGap,
                $"{two[index].Label} and {two[index - 2].Label} are in one row and only "
                + $"{Apart(two[index - 2], two[index])} px apart");
        }
    }

    [Fact]
    public void Staggering_costs_a_second_row_of_room()
    {
        var renderer = Renderer(ChartDimension.Y);
        renderer.TickLength = 4;
        renderer.LabelGap = 4;
        renderer.LabelMargin = 4;

        var one = renderer.MeasureGutter(Layout(0, 5, 10));

        renderer.StaggerLabels = true;

        // One more row of exactly the thickness the single row had, and nothing else: turning the labels off again
        // leaves the marks, so there is no second row to pay for when nothing is drawn.
        Assert.Equal(one + renderer.MeasureLabel("10").Width, renderer.MeasureGutter(Layout(0, 5, 10)), 6);

        renderer.ShowLabels = false;
        Assert.Equal(4, renderer.MeasureGutter(Layout(0, 5, 10)), 6);
    }

    [Fact]
    public void A_label_is_not_turned_past_vertical()
    {
        // Past a quarter turn either way a label reads upside down, and one nobody can read is not worth the room
        // it takes - so it is refused rather than drawn.
        var renderer = Renderer(ChartDimension.X);

        Assert.Equal(0, renderer.LabelRotation);
        Assert.False(renderer.StaggerLabels);

        renderer.LabelRotation = -90;
        Assert.Equal(-90, renderer.LabelRotation);

        foreach (var wrong in new[] { 90.5, -90.5, 180, double.NaN })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.LabelRotation = wrong);
        }
    }

    [Fact]
    public void Turning_or_staggering_the_labels_says_that_the_axis_changed()
    {
        // Both of them change how much room the axis asks for, and the room is worked out during layout - so a
        // host that sets one has to be answered with a layout and not only with a picture.
        var renderer = Renderer(ChartDimension.X);
        var said = 0;

        renderer.Changed += (_, _) => said++;

        renderer.LabelRotation = 45;
        renderer.StaggerLabels = true;

        Assert.Equal(2, said);

        // Set to what they already are, neither is a change: an axis that said otherwise would lay the chart out
        // again for nothing.
        renderer.LabelRotation = 45;
        renderer.StaggerLabels = true;

        Assert.Equal(2, said);
    }

    [Fact]
    public void A_title_takes_the_room_it_needs_beyond_the_labels()
    {
        // A title is one more line of writing on the far side of the labels, so the gutter grows by the gap and by
        // the thickness of the writing itself.
        var layout = Layout(0, 5, 10);
        var axis = new LinearAxis();
        var renderer = new AxisRenderer(axis, ChartDimension.Y);
        renderer.TickLength = 4;
        renderer.LabelGap = 4;
        renderer.LabelMargin = 4;
        renderer.TitleGap = 6;

        var bare = renderer.MeasureGutter(layout);
        var title = renderer.MeasureLabel("Headcount").Height;

        Assert.True(title > 0, "a title of nine letters should be some pixels thick");

        axis.Title = "Headcount";

        Assert.Equal(bare + 6 + title, renderer.MeasureGutter(layout), 6);

        // One line thick on either axis: a title across the chart is written as it is, and one up the chart is turned
        // a quarter turn, which swaps the two extents of the box it needs.
        var across = new LinearAxis();
        var horizontal = new AxisRenderer(across, ChartDimension.X);
        horizontal.TickLength = 4;
        horizontal.LabelGap = 4;
        horizontal.LabelMargin = 4;
        horizontal.TitleGap = 6;

        var bareAcross = horizontal.MeasureGutter(layout);

        across.Title = "Headcount";

        Assert.Equal(bareAcross + 6 + title, horizontal.MeasureGutter(layout), 6);
    }

    [Fact]
    public void A_title_is_room_taken_even_where_the_labels_are_not()
    {
        // The labels are the chart's readings of itself and the title is what the axis measures: turning one off
        // says nothing about the other, so the room the title asked for is still taken - with nothing else in it.
        var axis = new LinearAxis { Title = "Headcount" };
        var renderer = new AxisRenderer(axis, ChartDimension.Y);
        renderer.TickLength = 4;
        renderer.TitleGap = 6;
        renderer.ShowLabels = false;

        Assert.Equal(4 + 6 + renderer.MeasureLabel("Headcount").Height, renderer.MeasureGutter(Layout(0, 5, 10)), 6);
    }

    [Fact]
    public void What_an_axis_says_changed_is_what_its_renderer_says_changed()
    {
        // A chart listens to the renderer, and an axis' own settings take room the same way the renderer's do: a
        // title, a minimum, a step that puts wider labels under the axis. Without this the axis was worked out again
        // and painted again but never laid out again, which is a title drawn in the gutter measured for no title.
        var axis = new LinearAxis();
        var renderer = new AxisRenderer(axis, ChartDimension.X);
        var said = 0;

        renderer.Changed += (_, _) => said++;

        axis.Title = "Seconds";
        axis.Interval = 5;

        Assert.Equal(2, said);
    }
}
