namespace AvCharts.Charts;

/// <summary>
/// The steps a clock takes, in milliseconds: what a time axis and a duration axis both choose their grid lines
/// from.
/// </summary>
/// <remarks>
/// The shape is the clock's rather than the decimal ladder's - 10, 20 and 50 microseconds, then the same in
/// milliseconds, then 1, 2, 5, 10, 15 and 30 seconds, minutes and hours, then whole days. Past a week it gives
/// up: a month is not a fixed number of milliseconds, so whoever asked decides what to do instead - a
/// <see cref="DateTimeAxis"/> carries on into months and years, and a <see cref="TimeSpanAxis"/>, which has no
/// calendar to speak of, stays in days.
/// </remarks>
internal static class ClockLadder
{
    /// <summary>How many milliseconds a day is; the unit beyond the ladder.</summary>
    public const double PerDay = 86_400_000;

    private static readonly double[] Steps =
    [
        // microseconds
        0.01, 0.02, 0.05, 0.1, 0.2, 0.5,

        // milliseconds
        1, 2, 5, 10, 20, 50, 100, 200, 500,

        // seconds
        1_000, 2_000, 5_000, 10_000, 15_000, 30_000,

        // minutes
        60_000, 120_000, 300_000, 600_000, 900_000, 1_800_000,

        // hours
        3_600_000, 7_200_000, 10_800_000, 21_600_000, 43_200_000,

        // days
        PerDay, 2 * PerDay, 7 * PerDay,
    ];

    /// <summary>The smallest step at or above a wanted one, or null when the wanted one is beyond the ladder.</summary>
    /// <param name="targetMilliseconds">The gap that would be ideal, in milliseconds.</param>
    /// <returns>The step, in milliseconds.</returns>
    public static double? Nice(double targetMilliseconds)
    {
        foreach (var step in Steps)
        {
            if (step >= targetMilliseconds * (1 - 1e-9))
            {
                return step;
            }
        }

        return null;
    }

    /// <summary>How many decimals a label needs to tell one step from the next.</summary>
    /// <param name="intervalMilliseconds">The gap between grid lines.</param>
    /// <returns>0, 3 or 6, for seconds, milliseconds or microseconds.</returns>
    public static int Decimals(double intervalMilliseconds) =>
        intervalMilliseconds >= 1_000 ? 0 : intervalMilliseconds >= 1 ? 3 : 6;
}
