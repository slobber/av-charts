using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class TimeSpanAxisTests
{
    [Fact]
    public void A_duration_arrives_as_a_number_of_milliseconds()
    {
        var axis = new TimeSpanAxis();

        Assert.True(axis.TryToUnit((ChartValue)1_500.0, out var unit));
        Assert.Equal(1_500, unit);
        Assert.False(axis.TryToUnit(ChartValue.Empty, out _));
    }

    [Fact]
    public void The_ladder_is_the_clocks()
    {
        var axis = new TimeSpanAxis();

        Assert.Equal(0.01, axis.Resolve([new BoundedValue(0), new BoundedValue(0.1)], 600).MajorInterval, 9);
        Assert.Equal(10, axis.Resolve([new BoundedValue(0), new BoundedValue(100)], 600).MajorInterval, 9);
        Assert.Equal(5_000, axis.Resolve([new BoundedValue(0), new BoundedValue(50_000)], 600).MajorInterval, 9);
    }

    [Fact]
    public void Labels_read_like_a_stopwatch()
    {
        var axis = new TimeSpanAxis { Interval = 1 };
        var coarse = new TimeSpanAxis { Interval = 1_000 };
        var long_ = new TimeSpanAxis { Interval = 86_400_000 };

        Assert.Equal("01:02:03.456", axis.FormatValue(TimeSpan.FromHours(1).TotalMilliseconds + TimeSpan.FromMinutes(2).TotalMilliseconds + 3_456));
        Assert.Equal("01:02:03", coarse.FormatValue(3_723_000));
        Assert.Equal("3.01:02:03", long_.FormatValue(TimeSpan.FromDays(3).TotalMilliseconds + 3_723_000));
    }

    [Fact]
    public void Labels_are_as_precise_as_the_step_needs()
    {
        var microseconds = new TimeSpanAxis { Interval = 0.01 };
        var milliseconds = new TimeSpanAxis { Interval = 1 };

        Assert.Equal("00:00:00.000010", microseconds.FormatValue(0.01));
        Assert.Equal("00:00:00.010", milliseconds.FormatValue(10));
    }

    [Fact]
    public void Without_an_interval_a_label_still_knows_how_precise_to_be()
    {
        var axis = new TimeSpanAxis();

        Assert.Equal("00:00:05", axis.FormatValue(5_000));
        Assert.Equal("00:00:05.500", axis.FormatValue(5_500));
        // Half a millisecond is five hundred microseconds, and the label says so.
        Assert.Equal("00:00:00.000500", axis.FormatValue(0.5));
    }

    [Fact]
    public void A_negative_duration_is_placed_like_any_other_number()
    {
        var axis = new TimeSpanAxis { Minimum = -1_000, Maximum = 1_000, Interval = 500 };

        var layout = axis.Resolve([], 600);

        Assert.Equal(-1_000, layout.UnitRange.Minimum);
        Assert.Equal(5, layout.MajorTicks.Count);
    }
}
