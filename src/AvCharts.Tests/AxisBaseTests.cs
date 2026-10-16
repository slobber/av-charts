using Avalonia;
using AvCharts.Axes;
using AvCharts.Data;
using AvCharts.Elements;
using AvCharts.Transforms;
using Xunit;

namespace AvCharts.Tests;

public class AxisBaseTests
{
    private static BoundedValue[] Values(params double[] values) =>
        [.. values.Select(value => new BoundedValue(value))];

    private static AxisLayout Fit(LinearAxis axis, double lengthInPixels, params double[] values) =>
        axis.Resolve(Values(values), lengthInPixels);

    [Fact]
    public void The_range_follows_the_data_and_honours_the_room_a_marker_asked_for()
    {
        var layout = Fit(new LinearAxis(), 600, 5, 10);

        Assert.Equal(5, layout.UnitRange.Minimum);
        Assert.Equal(10, layout.UnitRange.Maximum);

        var withMargin = new LinearAxis().Resolve([new BoundedValue(10, UpperMargin: 2)], 600);

        Assert.Equal(12, withMargin.UnitRange.Maximum);
    }

    [Fact]
    public void Minimum_and_maximum_beat_the_data()
    {
        var layout = Fit(new LinearAxis { Minimum = -5, Maximum = 20 }, 600, 5, 10);

        Assert.Equal(-5, layout.UnitRange.Minimum);
        Assert.Equal(20, layout.UnitRange.Maximum);
    }

    [Fact]
    public void Only_one_end_set_lets_the_data_decide_the_other()
    {
        var layout = Fit(new LinearAxis { Minimum = -5 }, 600, 5, 10);

        Assert.Equal(-5, layout.UnitRange.Minimum);
        Assert.Equal(10, layout.UnitRange.Maximum);
    }

    [Fact]
    public void An_auto_interval_lands_on_a_readable_number_and_never_packs_the_grid_denser_than_the_pitch()
    {
        // 600 px of plot, 60 px of wanted pitch -> ten gaps over a range of 37, so 3.7 units each, which
        // snaps up to 5 and gives an 81 px pitch: never denser than asked for.
        var axis = new LinearAxis();
        var layout = Fit(axis, 600, 0, 37);

        Assert.Equal(5, layout.MajorInterval);
        Assert.True(layout.MajorInterval / layout.UnitRange.Length * 600 >= axis.MajorGridPitch);
    }

    [Fact]
    public void An_explicit_interval_is_used_as_given()
    {
        var layout = Fit(new LinearAxis { Interval = 3 }, 600, 0, 20);

        Assert.Equal(3, layout.MajorInterval);
    }

    [Fact]
    public void Ticks_start_at_the_first_multiple_inside_the_range_and_fill_it()
    {
        var layout = Fit(new LinearAxis { Interval = 5, Minimum = 3, Maximum = 22 }, 600);

        Assert.Equal([5d, 10d, 15d, 20d], layout.MajorTicks);
    }

    [Fact]
    public void Ticks_reach_the_end_of_the_range_even_when_the_data_stops_earlier()
    {
        // The whole point of drawing the grid over the range rather than over the data: a range that a
        // fixed scale stretched shows grid lines rather than empty space.
        var axis = new LinearAxis { Interval = 10, Minimum = 0, Maximum = 95 };
        var layout = Fit(axis, 600);

        Assert.Equal([0d, 10d, 20d, 30d, 40d, 50d, 60d, 70d, 80d, 90d], layout.MajorTicks);
    }

    [Fact]
    public void Separators_replace_the_generated_ticks_and_the_range_still_filters_them()
    {
        var axis = new LinearAxis { Separators = [0, 25, 100] };
        var layout = Fit(axis, 600, 0, 50);

        Assert.Equal([0d, 25d], layout.MajorTicks);
    }

    [Fact]
    public void The_minor_interval_follows_the_ratio_of_the_two_pitches()
    {
        var layout = Fit(new LinearAxis { Interval = 5 }, 600, 0, 20);

        Assert.Equal(1, layout.MinorInterval);
        Assert.Equal([0d, 1d, 2d, 3d, 4d, 5d], layout.MinorTicks.Take(6));
    }

    [Fact]
    public void Without_data_or_settings_there_is_nothing_to_cover()
    {
        var layout = Fit(new LinearAxis(), 600);

        Assert.True(layout.UnitRange.IsEmpty);
        Assert.Empty(layout.MajorTicks);
        Assert.Equal(0, layout.MajorInterval);
    }

    [Fact]
    public void A_single_value_gives_an_empty_range_rather_than_an_invented_window()
    {
        var layout = Fit(new LinearAxis(), 600, 7);

        Assert.True(layout.UnitRange.IsEmpty);
        Assert.Equal(7, layout.UnitRange.Minimum);
        Assert.Empty(layout.MajorTicks);
    }

    [Fact]
    public void A_fixed_scale_turns_a_wider_plot_into_more_data_rather_than_wider_spacing()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 2, Minimum = 12, Interval = 5 };

        var layout = axis.Resolve([], 600);

        // The window starts where it was told to - an awkward anchor and all, so that dragging a fixed scale
        // moves it by what was dragged rather than in whole cells - and it ends where the scale says: 600 px at
        // 2 px per unit. The grid lines are on the interval's multiples within it.
        Assert.Equal(12, layout.UnitRange.Minimum);
        Assert.Equal(312, layout.UnitRange.Maximum);
        Assert.Equal(15, layout.MajorTicks[0]);
    }

    [Fact]
    public void A_fixed_scale_ignores_maximum_and_says_so()
    {
        var axis = new LinearAxis
        {
            ScaleMode = AxisScaleMode.Fixed,
            PixelsPerUnit = 1,
            Minimum = 0,
            Maximum = 999,
        };

        var layout = axis.Resolve([], 300);

        Assert.True(axis.IsMaximumIgnored);
        Assert.Equal(300, layout.UnitRange.Maximum);
    }

    [Fact]
    public void A_fixed_scale_without_a_usable_scale_behaves_like_a_fitting_one()
    {
        var axis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 0 };

        var layout = Fit(axis, 600, 0, 100);

        Assert.Equal(0, layout.UnitRange.Minimum);
        Assert.Equal(100, layout.UnitRange.Maximum);
    }

    [Fact]
    public void Changing_a_setting_reaches_the_transform_that_holds_the_axis()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 600, 300) };
        var axis = new LinearAxis();
        transform.SetAxis(ChartDimension.X, axis);
        transform.AddElement(new BoundsElement(0, 20));

        // 600 px of plot at a 60 px pitch wants about ten gaps over a range of 20, so two units each.
        Assert.Equal(2, transform.GetLayout(ChartDimension.X).MajorInterval);

        axis.Interval = 4;

        Assert.Equal(4, transform.GetLayout(ChartDimension.X).MajorInterval);
    }

    [Fact]
    public void A_step_walks_its_own_grid()
    {
        var step = new EvenStep(5);

        Assert.Equal(15, step.Floor(18.5));
        Assert.Equal(20, step.Advance(15));
        Assert.Equal(20, step.Ceiling(18.5));
        Assert.Equal(15, step.Ceiling(15));
    }

    [Fact]
    public void A_line_that_is_meant_to_read_as_a_round_number_reads_as_one()
    {
        // Ticks are whole multiples of the step, because a running total drifts: walking a step of 0.4 up from
        // -1.6 lands the line that should be zero on -1.110223022E-16, and the label says exactly that. A chart
        // of a signal that swings either side of nought is where it shows, which is how it was found.
        var axis = new LinearAxis { Minimum = -1.6, Maximum = 1.6, Interval = 0.4 };
        var ticks = axis.Resolve([], 200).MajorTicks;

        var zero = ticks.Single(tick => Math.Abs(tick) < 0.2);

        Assert.Equal(0, zero);
        Assert.Equal("0", axis.FormatValue(zero));
    }

    [Fact]
    public void A_step_of_no_length_draws_nothing()
    {
        Assert.Equal(0, AxisStep.None.Length);
        Assert.False(AxisStep.None.Advance(3) > 3);
        Assert.False(new EvenStep(-1).Advance(3) > 3);
    }

    [Fact]
    public void One_axis_instance_can_drive_several_charts_at_once()
    {
        var shared = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 10, Minimum = 0 };

        var narrow = new DataTransform { PlotBounds = new Rect(0, 0, 100, 100) };
        var wide = new DataTransform { PlotBounds = new Rect(0, 0, 200, 100) };
        narrow.SetAxis(ChartDimension.X, shared);
        wide.SetAxis(ChartDimension.X, shared);

        Assert.Equal(10, narrow.GetLayout(ChartDimension.X).UnitRange.Maximum);
        Assert.Equal(20, wide.GetLayout(ChartDimension.X).UnitRange.Maximum);
        Assert.Equal(shared, narrow.GetAxis(ChartDimension.X));
    }

    private sealed class BoundsElement(double minimum, double maximum) : IChartElement
    {
        public void DescribeData(DataDescription description)
        {
            description.Add(ChartDimension.X, new BoundedValue(minimum));
            description.Add(ChartDimension.X, new BoundedValue(maximum));
        }

        public void MappingChanged()
        {
        }
    }
}
