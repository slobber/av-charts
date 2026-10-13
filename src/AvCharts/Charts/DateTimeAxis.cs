using System.Globalization;

namespace AvCharts.Charts;

/// <summary>
/// An axis of instants, measured in milliseconds since 1970-01-01T00:00:00.
/// </summary>
/// <remarks>
/// <para>
/// Where the count starts is not a detail. A double carries about 240 nanoseconds at the magnitudes this axis
/// works in, so a step of ten microseconds is comfortably representable; counting from the year 1 instead would
/// leave roughly fifteen microseconds of noise, which is the same order as the steps a fast capture wants. The
/// difference between the two instants is taken in whole ticks before anything is divided, so no precision is
/// lost to subtracting two large numbers.
/// </para>
/// <para>
/// The ladder walks in the steps a clock does - 10, 20 and 50 microseconds, then the same shape in
/// milliseconds, then 1, 2, 5, 10, 15 and 30 seconds, minutes, hours and days. Past a week it falls back to
/// whole days, because a month is not a fixed number of milliseconds and a calendar-aware tick generator is a
/// refinement this version does not pretend to have.
/// </para>
/// <para>
/// <see cref="AxisBase.Minimum"/>, <see cref="AxisBase.Maximum"/> and <see cref="AxisBase.Interval"/> are in
/// those milliseconds, as <see cref="AxisBase"/> defines them: <c>Interval = 10</c> is ten milliseconds,
/// <c>Interval = 0.01</c> is ten microseconds.
/// </para>
/// </remarks>
public class DateTimeAxis : AxisBase
{
    /// <summary>The instant the count starts from, in ticks; taking the difference in ticks keeps it exact.</summary>
    private static readonly long TicksAtOrigin = DateTime.UnixEpoch.Ticks;

    /// <inheritdoc />
    public override ChartValueKind ValueKind => ChartValueKind.DateTime;

    /// <summary>Measures an instant in the unit space of this axis.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>Milliseconds since 1970-01-01T00:00:00, as a number.</returns>
    public static double ToMilliseconds(DateTimeOffset value) =>
        (value.UtcTicks - TicksAtOrigin) / (double)TimeSpan.TicksPerMillisecond;

    /// <summary>Turns a position in unit space back into an instant.</summary>
    /// <param name="unit">Milliseconds since 1970-01-01T00:00:00.</param>
    /// <returns>The instant, to the nearest tick.</returns>
    public static DateTimeOffset ToInstant(double unit) =>
        new(TicksAtOrigin + (long)Math.Round(unit * TimeSpan.TicksPerMillisecond), TimeSpan.Zero);

    /// <inheritdoc />
    public override bool TryToUnit(ChartValue value, out double unit)
    {
        if (value.Kind == ChartValueKind.DateTime)
        {
            unit = ToMilliseconds(value.Instant);
            return true;
        }

        unit = 0;
        return false;
    }

    /// <inheritdoc />
    public override ChartValue FromUnit(double unit) => ChartValue.FromDateTimeOffset(ToInstant(unit));

    /// <summary>
    /// The clock the labels are drawn in. Left unset, each instant is labelled in the offset it arrived with,
    /// which is what keeps a wall-clock reading looking like the reading; set it, and every label is drawn in
    /// one offset - what a chart shared between sites wants.
    /// </summary>
    public TimeSpan? DisplayOffset { get; set; }

    /// <inheritdoc />
    public override string FormatValue(double unit)
    {
        var instant = ToInstant(unit);
        var shown = DisplayOffset is { } offset ? instant.ToOffset(offset) : instant;

        return Interval is { } interval ? Format(shown, interval) : Format(shown);
    }

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

    /// <summary>
    /// Writes an instant the way an axis stepping by <paramref name="intervalMs"/> should read it: only as much
    /// of the clock as the step distinguishes.
    /// </summary>
    private static string Format(DateTimeOffset instant, double intervalMs)
    {
        if (intervalMs >= ClockLadder.PerDay)
        {
            return instant.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (intervalMs >= 60_000)
        {
            return instant.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        if (intervalMs >= 1_000)
        {
            return instant.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return intervalMs >= 1
            ? instant.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : instant.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes an instant when the interval was not given: as much of the clock as the instant itself carries.
    /// </summary>
    private static string Format(DateTimeOffset instant)
    {
        if (instant.Ticks % TimeSpan.TicksPerDay == 0)
        {
            return instant.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (instant.Ticks % TimeSpan.TicksPerSecond == 0)
        {
            return instant.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return instant.Ticks % TimeSpan.TicksPerMillisecond == 0
            ? instant.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : instant.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    }
}
