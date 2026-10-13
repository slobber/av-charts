using System.Globalization;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class DateTimeAxisTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 10, 23, 45, TimeSpan.Zero);

    /// <summary>A range from an instant to that instant some whole days later, in unit space.</summary>
    private static BoundedValue[] Span(DateTimeOffset from, double days) =>
    [
        new BoundedValue(DateTimeAxis.ToMilliseconds(from)),
        new BoundedValue(DateTimeAxis.ToMilliseconds(from) + (days * 86_400_000)),
    ];

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
    public void A_span_past_a_fortnight_steps_on_the_first_of_the_month()
    {
        var axis = new DateTimeAxis();

        // Half a year over 600 pixels wants gaps of about eighteen days. Eighteen days is past the fortnight
        // the ladder stops at, so the lines come from the calendar: one a month, on the first.
        var layout = axis.Resolve(Span(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), 180), 600);

        Assert.Equal([4, 5, 6, 7, 8], layout.MajorTicks.Select(unit => DateTimeAxis.ToInstant(unit).Month));
        Assert.All(
            layout.MajorTicks,
            unit =>
            {
                var instant = DateTimeAxis.ToInstant(unit);

                Assert.Equal(1, instant.Day);
                Assert.Equal(TimeSpan.Zero, instant.TimeOfDay);
            });
    }

    [Fact]
    public void A_month_boundary_survives_the_trip_through_unit_space()
    {
        // A calendar step is the one place where being a tick out changes the answer: a line a microsecond
        // before midnight would be read as the month before and would be labelled with it. Unit space is a
        // double of milliseconds, and 2026 is a billion of them, so this is not free - it holds because a
        // midnight is a whole number of days, and those counts are multiples of powers of two, which are
        // exactly what a double keeps. Measured over four centuries; the loop is the guard.
        for (var year = 1970; year < 2400; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var midnight = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
                var roundTripped = DateTimeAxis.ToInstant(DateTimeAxis.ToMilliseconds(midnight));

                Assert.Equal(midnight.UtcDateTime, roundTripped.UtcDateTime);
            }
        }
    }

    [Fact]
    public void Every_label_on_a_monthly_axis_says_a_month()
    {
        var axis = new DateTimeAxis();

        // Two months apart, across a New Year: the January line is a month like the others and has to read
        // like one, rather than collapsing to a bare year the way an instant-precision label would.
        var layout = axis.Resolve(Span(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), 340), 600);

        var labels = layout.MajorTicks.Select(unit => axis.FormatValue(unit, layout.MajorStep));

        Assert.Equal(["2026-05", "2026-07", "2026-09", "2026-11", "2027-01"], labels);
    }

    [Fact]
    public void A_span_of_years_steps_on_new_years_day()
    {
        var axis = new DateTimeAxis();

        // Six years over 600 pixels wants gaps of about seven months, which is longer than the half year the
        // month ladder stops at: from there the lines are whole years.
        var layout = axis.Resolve(Span(new DateTimeOffset(2023, 2, 14, 0, 0, 0, TimeSpan.Zero), 2_190), 600);

        Assert.All(
            layout.MajorTicks,
            unit =>
            {
                var instant = DateTimeAxis.ToInstant(unit);

                Assert.Equal(1, instant.Month);
                Assert.Equal(1, instant.Day);
                Assert.Equal(TimeSpan.Zero, instant.TimeOfDay);
            });
        Assert.All(
            layout.MajorTicks.Zip(layout.MajorTicks.Skip(1)),
            pair => Assert.Equal(1, DateTimeAxis.ToInstant(pair.Second).Year - DateTimeAxis.ToInstant(pair.First).Year));

        var dates = layout.MajorTicks.Select(unit => DateTimeAxis.ToInstant(unit));
        var labels = layout.MajorTicks.Select(unit => axis.FormatValue(unit, layout.MajorStep));

        // A year is named by its number: four characters, the same as the year it stands for.
        Assert.Equal(dates.Select(date => date.Year.ToString("D4", CultureInfo.InvariantCulture)), labels);
    }

    [Fact]
    public void A_span_between_a_week_and_a_fortnight_still_steps_in_days()
    {
        var axis = new DateTimeAxis();

        // Three months over 600 pixels wants gaps of about nine days: two weeks still reads as days, and the
        // calendar only takes over once a fortnight is too fine.
        var layout = axis.Resolve(Span(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), 90), 600);

        Assert.Equal(14 * 86_400_000d, layout.MajorInterval, 9);
        Assert.Equal(10, axis.FormatValue(layout.MajorTicks[0], layout.MajorStep).Length);
    }

    [Fact]
    public void A_calendar_step_draws_no_finer_lines()
    {
        var axis = new DateTimeAxis();
        var layout = axis.Resolve(Span(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), 180), 600);

        // A fifth of a month is not a day or a week or anything else worth a line, so there are none.
        Assert.Equal(0, layout.MinorInterval);
        Assert.Empty(layout.MinorTicks);
    }

    [Fact]
    public void An_interval_the_caller_set_is_taken_literally()
    {
        var axis = new DateTimeAxis { Interval = 30 * 86_400_000d };

        var layout = axis.Resolve(Span(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), 180), 600);

        Assert.Equal(30 * 86_400_000d, layout.MajorInterval, 9);

        // Thirty days is not a month: a grid of them drifts off the first, and the labels say dates.
        Assert.Contains(layout.MajorTicks, unit => DateTimeAxis.ToInstant(unit).Day != 1);
        Assert.Equal(10, axis.FormatValue(layout.MajorTicks[0], layout.MajorStep).Length);
    }

    [Fact]
    public void A_fixed_scale_starts_the_window_on_the_calendar_grid()
    {
        var axis = new DateTimeAxis
        {
            ScaleMode = AxisScaleMode.Fixed,
            PixelsPerUnit = 600 / (180 * 86_400_000d),
            Minimum = DateTimeAxis.ToMilliseconds(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero)),
        };

        var layout = axis.Resolve([], 600);

        // The window may not start in the middle of a month any more than a fixed numeric scale may start
        // between two of its lines.
        var start = DateTimeAxis.ToInstant(layout.UnitRange.Minimum);

        Assert.Equal(new DateTime(2026, 3, 1), start.UtcDateTime);
    }

    [Fact]
    public void A_display_offset_puts_the_grid_on_the_same_clock_as_the_labels()
    {
        var offset = TimeSpan.FromHours(8);
        var axis = new DateTimeAxis { DisplayOffset = offset };

        var layout = axis.Resolve(Span(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), 180), 600);

        // The lines are the firsts of months in the clock the reader is in, which is not midnight in UTC...
        Assert.All(
            layout.MajorTicks,
            unit =>
            {
                var local = DateTimeAxis.ToInstant(unit).ToOffset(offset);

                Assert.Equal(1, local.Day);
                Assert.Equal(TimeSpan.Zero, local.TimeOfDay);
            });

        // ...and the labels are read in that same clock, so the two agree.
        Assert.Equal(
            ["2026-04", "2026-05", "2026-06", "2026-07", "2026-08"],
            layout.MajorTicks.Select(unit => axis.FormatValue(unit, layout.MajorStep)));
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
