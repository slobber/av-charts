using AvCharts.Axes;


using AvCharts.Data;


using Xunit;

namespace AvCharts.Tests;

public class LogAxisTests
{
    [Fact]
    public void A_log_axis_measures_decades()
    {
        var axis = new LogAxis();

        Assert.True(axis.TryToUnit((ChartValue)100.0, out var hundred));
        Assert.Equal(2, hundred, 9);

        Assert.True(axis.TryToUnit((ChartValue)0.1, out var tenth));
        Assert.Equal(-1, tenth, 9);

        Assert.True(axis.TryToUnit((ChartValue)1.0, out var one));
        Assert.Equal(0, one, 9);
    }

    [Fact]
    public void Zero_and_negative_values_have_no_place_on_a_log_axis()
    {
        var axis = new LogAxis();

        Assert.False(axis.TryToUnit((ChartValue)0.0, out _));
        Assert.False(axis.TryToUnit((ChartValue)(-5.0), out _));
        Assert.False(axis.TryToUnit(ChartValue.Empty, out _));
        Assert.False(axis.TryToUnit("north", out _));
    }

    [Fact]
    public void Values_round_trip()
    {
        var axis = new LogAxis();

        Assert.True(axis.TryToUnit((ChartValue)250.0, out var unit));

        var back = axis.FromUnit(unit);

        Assert.True(back.TryGetNumber(out var number));
        Assert.Equal(250, number, 6);
    }

    [Fact]
    public void Grid_lines_land_on_powers_of_ten()
    {
        var axis = new LogAxis { Minimum = 0, Maximum = 3, Interval = 1 };

        var layout = axis.Resolve([], 600);

        Assert.Equal([0d, 1d, 2d, 3d], layout.MajorTicks);
        Assert.Equal(["1", "10", "100", "1000"], layout.MajorTicks.Select(axis.FormatValue));
    }

    [Fact]
    public void The_grid_never_gets_finer_than_one_decade()
    {
        var axis = new LogAxis();

        // A tenth of a decade of data wants a hundredth of a decade per gap, and still gets one decade.
        var layout = axis.Resolve([new BoundedValue(0), new BoundedValue(0.1)], 600);

        Assert.Equal(1, layout.MajorInterval);
    }

    [Fact]
    public void A_wide_span_steps_several_decades_at_a_time()
    {
        var axis = new LogAxis();

        // Forty decades across ten gaps wants four each, and the ladder offers five.
        var layout = axis.Resolve([new BoundedValue(-20), new BoundedValue(20)], 600);

        Assert.Equal(5, layout.MajorInterval);
    }
}
