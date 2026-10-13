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
}
