using System.Globalization;


using AvCharts.Data;



namespace AvCharts.Axes;

/// <summary>
/// An axis of numbers that grow by factors rather than by steps: its unit space is the exponent.
/// </summary>
/// <remarks>
/// <para>
/// A value of 100 is two units and a value of 0.1 is minus one, so equal distances on screen are equal ratios
/// in the data. That is the whole point of a logarithmic axis, and it is also why zero and negative values
/// cannot be placed on one - they are reported as missing rather than pushed somewhere convenient, which keeps
/// the null policy in the design brief intact.
/// </para>
/// <para>
/// Grid lines land on powers of ten, one, two or five decades apart. A span narrower than a decade therefore
/// gets very few lines: uniform intervals are what the tick generator does today, and sub-decade lines - the
/// 1, 2, 5 inside each decade that a printed log chart shows - need a generator that is not uniform. Recorded
/// rather than faked.
/// </para>
/// </remarks>
public class LogAxis : AxisBase
{
    /// <summary>The steps a log axis takes, in decades.</summary>
    private static readonly double[] DecadeLadder = [1, 2, 5];

    /// <inheritdoc />
    public override ChartValueKind ValueKind => ChartValueKind.Number;

    /// <inheritdoc />
    public override bool TryToUnit(ChartValue value, out double unit)
    {
        unit = 0;

        if (!value.TryGetNumber(out var number) || !(number > 0))
        {
            return false;
        }

        unit = Math.Log10(number);
        return true;
    }

    /// <inheritdoc />
    public override ChartValue FromUnit(double unit) => Math.Pow(10, unit);

    /// <inheritdoc />
    public override string FormatValue(double unit) =>
        Math.Pow(10, unit).ToString("G6", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override double NiceInterval(double target)
    {
        // One decade is the finest grid a log axis draws, so a span narrower than that still gets one.
        if (!(target > 0) || double.IsInfinity(target))
        {
            return 1;
        }

        var scale = Math.Pow(10, Math.Floor(Math.Log10(target)));

        foreach (var step in DecadeLadder)
        {
            var candidate = step * scale;

            if (candidate >= target * (1 - 1e-9))
            {
                return Math.Max(1, candidate);
            }
        }

        return Math.Max(1, 10 * scale);
    }
}
