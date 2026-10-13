using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class DateTimeAxisTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 10, 23, 45, TimeSpan.Zero);

    [Fact]
    public void A_step_of_ten_microseconds_survives_the_trip_through_unit_space()
    {
        var axis = new DateTimeAxis();
        var instant = Start.AddTicks(TimeSpan.TicksPerMillisecond * 7 + 1234);

        Assert.True(axis.TryToUnit(instant, out var first));
        Assert.True(axis.TryToUnit(instant.AddTicks(TimeSpan.TicksPerMillisecond / 100), out var second));

        // Ten microseconds is a hundredth of a millisecond. At the magnitudes of 2026 a double carries about
        // 240 nanoseconds, so the step comes out slightly off - measured at ten nanoseconds, which is a
        // thousandth of the step, and a hundred times finer than the resolution being asked for.
        var step = second - first;
        var errorInMicroseconds = Math.Abs(step - 0.01) * 1_000;

        Assert.True(
            errorInMicroseconds < 0.1,
            $"a ten microsecond step came out {step} ms apart, off by {errorInMicroseconds} microseconds");
    }

    [Fact]
    public void A_microsecond_is_still_a_microsecond_at_the_magnitudes_of_2026()
    {
        var axis = new DateTimeAxis();
        var instant = new DateTimeOffset(2026, 10, 13, 10, 23, 45, TimeSpan.Zero);

        Assert.True(axis.TryToUnit(instant, out var first));
        Assert.True(axis.TryToUnit(instant.AddTicks(10), out var second));

        // One microsecond is ten ticks, and it has to survive being measured a billion milliseconds from the
        // origin: the noise floor is around a quarter of a microsecond, so this is the tightest step there is.
        var measuredMicroseconds = (second - first) * 1_000;

        Assert.True(
            Math.Abs(measuredMicroseconds - 1) < 0.5,
            $"a one microsecond step measured {measuredMicroseconds} microseconds");
    }

    [Fact]
    public void Instants_round_trip_to_within_a_fraction_of_a_microsecond()
    {
        var instant = new DateTimeOffset(2026, 10, 13, 10, 23, 45, 678, TimeSpan.Zero);

        var roundTripped = DateTimeAxis.ToInstant(DateTimeAxis.ToMilliseconds(instant));

        Assert.True(
            Math.Abs((roundTripped - instant).Ticks) < TimeSpan.TicksPerMillisecond / 10,
            $"off by {(roundTripped - instant).Ticks} ticks, which is more than a tenth of a microsecond");
    }

    [Fact]
    public void The_ladder_has_steps_below_a_millisecond()
    {
        var axis = new DateTimeAxis();

        // A hundred microseconds of data, ten gaps wanted: the step lands on ten microseconds.
        var layout = axis.Resolve([new BoundedValue(0), new BoundedValue(0.1)], 600);

        Assert.Equal(0.01, layout.MajorInterval, 9);
    }

    [Fact]
    public void The_ladder_walks_in_the_steps_a_clock_does()
    {
        var axis = new DateTimeAxis();

        Assert.Equal(0.01, axis.Resolve([new BoundedValue(0), new BoundedValue(0.1)], 600).MajorInterval, 9);
        Assert.Equal(10, axis.Resolve([new BoundedValue(0), new BoundedValue(100)], 600).MajorInterval, 9);
        Assert.Equal(5_000, axis.Resolve([new BoundedValue(0), new BoundedValue(50_000)], 600).MajorInterval, 9);
        Assert.Equal(300_000, axis.Resolve([new BoundedValue(0), new BoundedValue(3_000_000)], 600).MajorInterval, 9);
    }

    [Fact]
    public void A_span_longer_than_a_week_steps_in_whole_days()
    {
        var axis = new DateTimeAxis();
        var layout = axis.Resolve([new BoundedValue(0), new BoundedValue(40 * 86_400_000d)], 600);

        Assert.Equal(0, layout.MajorInterval % 86_400_000, 9);
        Assert.True(layout.MajorInterval >= 40 * 86_400_000d / 10);
    }

    [Fact]
    public void Ticks_are_ten_microseconds_apart_when_the_step_is_ten_microseconds()
    {
        var axis = new DateTimeAxis { Minimum = 0, Maximum = 0.1, Interval = 0.01 };

        var layout = axis.Resolve([], 600);

        Assert.Equal(11, layout.MajorTicks.Count);
        Assert.Equal(0.01, layout.MajorTicks[1] - layout.MajorTicks[0], 9);
        Assert.Equal(0.1, layout.MajorTicks[^1], 9);
    }

    [Fact]
    public void Labels_show_only_as_much_of_the_clock_as_the_step_needs()
    {
        var microseconds = new DateTimeAxis { Interval = 0.01 };
        var seconds = new DateTimeAxis { Interval = 1_000 };
        var days = new DateTimeAxis { Interval = 86_400_000 };

        Assert.Equal("10:23:45.000000", microseconds.FormatValue(DateTimeAxis.ToMilliseconds(Start)));
        Assert.Equal("10:23:45", seconds.FormatValue(DateTimeAxis.ToMilliseconds(Start)));
        Assert.Equal("2026-10-13", days.FormatValue(DateTimeAxis.ToMilliseconds(Start)));
    }

    [Fact]
    public void Neighbouring_labels_at_a_ten_microsecond_step_stay_distinct()
    {
        var axis = new DateTimeAxis { Minimum = 0, Maximum = 0.1, Interval = 0.01 };

        var labels = axis.Resolve([], 600).MajorTicks.Select(axis.FormatValue).ToArray();

        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal("10:23:45.000000", axis.FormatValue(DateTimeAxis.ToMilliseconds(Start)));
    }

    [Fact]
    public void A_number_is_not_an_instant()
    {
        var axis = new DateTimeAxis();

        Assert.False(axis.TryToUnit(12.5, out _));
        Assert.False(axis.TryToUnit(ChartValue.Empty, out _));
    }
}
