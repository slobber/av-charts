using System.Globalization;

namespace AvCharts.Charts;

/// <summary>
/// An axis of plain numbers: its unit space is the value itself.
/// </summary>
/// <remarks>
/// Everything an axis does beyond converting values lives in <see cref="AxisBase"/>; this type is the
/// arithmetic that says numbers are their own units. A date axis measures milliseconds instead, a log axis
/// the exponent, and both of them derive from here only to take their ladder apart.
/// </remarks>
public class LinearAxis : AxisBase
{
    /// <inheritdoc />
    public override ChartValueKind ValueKind => ChartValueKind.Number;

    /// <inheritdoc />
    public override bool TryToUnit(ChartValue value, out double unit) => value.TryGetNumber(out unit);

    /// <inheritdoc />
    public override ChartValue FromUnit(double unit) => unit;

    /// <summary>
    /// Writes a number the way a tick should read it: short enough that the arithmetic cannot be seen.
    /// </summary>
    /// <param name="unit">The value.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// Nine significant digits rather than the shortest round-trippable form, which is the default and which
    /// prints the noise a grid line is made of: a step of 0.2 added three times is 0.6000000000000001, and a
    /// label seventeen characters long is both unreadable and, because the gutter is measured from the widest
    /// label, a fifth of the plot given away to it.
    /// </remarks>
    public override string FormatValue(double unit) => unit.ToString("G9", CultureInfo.InvariantCulture);
}
