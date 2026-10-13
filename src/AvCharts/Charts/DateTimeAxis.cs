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
/// milliseconds, then 1, 2, 5, 10, 15 and 30 seconds, minutes, hours and days, and a fortnight. Past that it
/// steps by the calendar: one, two, three or six months, then whole years. A grid of fixed days drifts across
/// the months it is meant to mark, while a line on the first of every month never does. A step a month or a
/// year long has no finer rung that means anything, so it draws no minor lines.
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

    /// <summary>The last evenly spaced rung before the calendar: a fortnight still reads as days, and 25 days does not.</summary>
    private const double FortnightMilliseconds = 14 * ClockLadder.PerDay;

    /// <summary>The average length of a Gregorian month: 30 days, 10 hours, 29 minutes and 6 seconds.</summary>
    /// <remarks>
    /// Only ever used to compare a wanted gap against a step and to report how long a step is on average.
    /// Never to place a line: that is the calendar's business, which is the whole point of this axis.
    /// </remarks>
    private const double AverageMonthMilliseconds = 2_629_746_000;

    /// <summary>The month steps a gap is chosen from before years take over: the divisions a quarter knows.</summary>
    private static readonly int[] MonthLadder = [1, 2, 3, 6];

    /// <summary>The years a gap is chosen from, one per rung of the decimal ladder.</summary>
    private static readonly double[] YearLadder = [1, 2, 5, 10];

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
    /// The clock the grid and the labels are drawn in. Left unset, each instant is labelled in the offset it
    /// arrived with - what keeps a wall-clock reading looking like the reading - and the calendar lines fall on
    /// months of UTC; set it, and both the lines and the labels use that one offset, which is what a chart
    /// shared between sites wants.
    /// </summary>
    public TimeSpan? DisplayOffset { get; set; }

    /// <inheritdoc />
    public override string FormatValue(double unit)
    {
        var shown = Shown(ToInstant(unit));

        return Interval is { } interval ? Format(shown, interval) : Format(shown);
    }

    /// <inheritdoc />
    public override string FormatValue(double unit, AxisStep step)
    {
        // A calendar step is the only thing that asks for a month or a year rather than a date: every line on
        // such an axis is the first of a month, so naming the day of the month would only repeat itself. Doing
        // it from the step rather than from each instant is what keeps every label on the axis the same shape -
        // a year step says "2026", and a month step says "2026-01" even for the January line.
        if (step is not CalendarStep calendar)
        {
            return base.FormatValue(unit, step);
        }

        var shown = Shown(ToInstant(unit));

        return calendar.Months >= 12
            ? shown.ToString("yyyy", CultureInfo.InvariantCulture)
            : shown.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    protected override AxisStep ResolveStep(double target)
    {
        if (ClockLadder.Nice(target) is { } clockStep)
        {
            return new EvenStep(clockStep);
        }

        if (target <= FortnightMilliseconds)
        {
            return new EvenStep(FortnightMilliseconds);
        }

        return MonthsFor(target) is { } months
            ? new CalendarStep(months, DisplayOffset ?? TimeSpan.Zero)

            // Nothing but a gap that is not a number reaches this far, and a grid over infinity is a grid of
            // nothing: no lines is the honest answer.
            : AxisStep.None;
    }

    /// <summary>The smallest calendar step at or above a wanted gap, in months: 1, 2, 3, 6, then whole years.</summary>
    /// <param name="targetMilliseconds">The gap that would be ideal, in milliseconds.</param>
    /// <returns>The months between lines, or null when the gap is not a number the calendar can cover.</returns>
    private static int? MonthsFor(double targetMilliseconds)
    {
        if (!(targetMilliseconds > 0) || double.IsInfinity(targetMilliseconds))
        {
            return null;
        }

        foreach (var months in MonthLadder)
        {
            if (months * AverageMonthMilliseconds >= targetMilliseconds * (1 - 1e-9))
            {
                return months;
            }
        }

        // Years, one per rung of the decimal ladder: 1, 2, 5, 10, and on up by decades.
        var wanted = targetMilliseconds / (12 * AverageMonthMilliseconds);
        var decade = Math.Pow(10, Math.Floor(Math.Log10(wanted)));

        foreach (var rung in YearLadder)
        {
            var years = rung * decade;

            if (years * 12 * AverageMonthMilliseconds >= targetMilliseconds * (1 - 1e-9))
            {
                var months = years * 12;
                return months <= int.MaxValue ? (int)months : null;
            }
        }

        return null;
    }

    /// <summary>The instant as the reader's clock reads it.</summary>
    /// <param name="instant">The instant.</param>
    /// <returns>The instant in the display offset, or as it arrived when none is set.</returns>
    private DateTimeOffset Shown(DateTimeOffset instant) =>
        DisplayOffset is { } offset ? instant.ToOffset(offset) : instant;

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

    /// <summary>
    /// A step that lands on the first of a month, <paramref name="months"/> months apart, read in one offset.
    /// </summary>
    /// <param name="months">The months between lines; twelve or more means the lines are whole years.</param>
    /// <param name="offset">The calendar the lines belong to: UTC, or whatever clock the axis shows.</param>
    private sealed class CalendarStep(int months, TimeSpan offset) : AxisStep
    {
        /// <summary>How many months apart the lines are, which is what a label has to know.</summary>
        public int Months { get; } = months;

        /// <inheritdoc />
        public override double Length => Months * AverageMonthMilliseconds;

        /// <inheritdoc />
        public override double Floor(double value) => ToMilliseconds(MonthStart(ToInstant(value), Months, offset, true));

        /// <inheritdoc />
        public override double Advance(double value) => ToMilliseconds(MonthStart(ToInstant(value), Months, offset, false));
    }

    /// <summary>The first of the month step that contains an instant - or the one <paramref name="months"/> along.</summary>
    /// <param name="instant">The instant to place.</param>
    /// <param name="months">The months between lines.</param>
    /// <param name="offset">The calendar to read the instant in.</param>
    /// <param name="alignedToStep">
    /// <see langword="true"/> to take the first of the month step the instant is in; <see langword="false"/> to
    /// take the first of the month step that follows it.
    /// </param>
    /// <returns>The first of that month, at midnight, in the given offset.</returns>
    private static DateTimeOffset MonthStart(DateTimeOffset instant, int months, TimeSpan offset, bool alignedToStep)
    {
        var local = instant.ToOffset(offset);

        // Months are counted from January of the year 1, so a step of three months lands on the quarters and a
        // step of twelve lands on a January whatever month the range happens to start in.
        var index = ((local.Year - 1) * 12) + local.Month - 1;

        index = alignedToStep
            ? (int)(Math.Floor(index / (double)months) * months)
            : index + months;

        // An instant outside the calendar the framework knows, which only nonsense data produces, snaps to the
        // nearest edge rather than throwing in the middle of a layout.
        index = Math.Clamp(index, 0, (9999 * 12) - 1);

        return new DateTimeOffset((index / 12) + 1, (index % 12) + 1, 1, 0, 0, 0, offset);
    }
}
