using System.Globalization;

namespace AvCharts.Charts;

/// <summary>
/// An axis of durations, measured in milliseconds.
/// </summary>
/// <remarks>
/// <para>
/// A duration arrives as a number of milliseconds - there is no duration kind in <see cref="ChartValue"/>, and
/// a plain number is what a selector produces naturally: <c>item =&gt; item.Elapsed.TotalMilliseconds</c>. What
/// this axis adds over a linear one is that the grid and the labels are the clock's: the ladder steps in
/// microseconds, milliseconds, seconds, minutes, hours and days, and a label reads <c>01:02:03.456</c> rather
/// than <c>3723456</c>.
/// </para>
/// <para>
/// It is not a <see cref="DateTimeAxis"/>: a duration has no date, so a span of three days is written
/// <c>3.01:02:03</c> and never as a day of the week.
/// </para>
/// </remarks>
public class TimeSpanAxis : AxisBase
{
    /// <inheritdoc />
    public override ChartValueKind ValueKind => ChartValueKind.Number;

    /// <inheritdoc />
    public override bool TryToUnit(ChartValue value, out double unit) => value.TryGetNumber(out unit);

    /// <inheritdoc />
    public override ChartValue FromUnit(double unit) => unit;

    /// <inheritdoc />
    public override string FormatValue(double unit) => Format(TimeSpan.FromMilliseconds(unit), Interval);

    /// <inheritdoc />
    protected override double NiceInterval(double target)
    {
        if (!(target > 0) || double.IsInfinity(target))
        {
            return 1;
        }

        // Longer than a week: whole days, on the plain ladder, because a month is not a fixed length.
        return ClockLadder.Nice(target) ?? base.NiceInterval(target / ClockLadder.PerDay) * ClockLadder.PerDay;
    }

    private static string Format(TimeSpan duration, double? intervalMs)
    {
        var interval = intervalMs ?? DurationOfLargestPlace(duration);

        // Days first: a span that long reads better as a count of days than as eighty hours.
        return duration.Days != 0 || interval >= ClockLadder.PerDay
            ? duration.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
            : ClockLadder.Decimals(interval) switch
            {
                0 => duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                3 => duration.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture),
                _ => duration.ToString(@"hh\:mm\:ss\.ffffff", CultureInfo.InvariantCulture),
            };
    }

    /// <summary>The largest whole unit a duration has, as milliseconds; used when no interval was set.</summary>
    private static double DurationOfLargestPlace(TimeSpan duration)
    {
        if (duration.Days != 0)
        {
            return ClockLadder.PerDay;
        }

        if (duration.Hours != 0 || duration.Minutes != 0)
        {
            return 1_000;
        }

        if (duration.Ticks % TimeSpan.TicksPerSecond == 0)
        {
            return 1_000;
        }

        return duration.Ticks % TimeSpan.TicksPerMillisecond == 0 ? 1 : 0.001;
    }
}
