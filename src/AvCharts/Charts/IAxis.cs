namespace AvCharts.Charts;

/// <summary>The stretch of unit space an axis currently covers, in ascending order.</summary>
/// <param name="Minimum">The lowest unit value on screen.</param>
/// <param name="Maximum">The highest unit value on screen.</param>
public readonly record struct AxisUnitRange(double Minimum, double Maximum)
{
    /// <summary>A range that covers nothing: the axis was given no data, or one value.</summary>
    public static AxisUnitRange Empty { get; } = new(0, 0);

    /// <summary>The width of the range in unit space.</summary>
    public double Length => Maximum - Minimum;

    /// <summary>
    /// True when the range cannot be scaled into: no data, a single value, or a NaN. Consumers map such a
    /// range to the middle of the plot rather than dividing by zero.
    /// </summary>
    public bool IsEmpty => !(Length > 0);

    /// <summary>Places a unit value in the range as a fraction, 0 at the minimum and 1 at the maximum.</summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The fraction; 0.5 when the range is empty.</returns>
    public double Normalize(double unit) => IsEmpty ? 0.5 : (unit - Minimum) / Length;

    /// <summary>Turns a fraction of the range back into a unit value.</summary>
    /// <param name="fraction">The fraction, 0 at the minimum.</param>
    /// <returns>The unit value.</returns>
    public double Denormalize(double fraction) => Minimum + (fraction * Length);
}

/// <summary>
/// What a transform needs from an axis: how to turn the values of one dimension into a single monotonic
/// number, and how much of that number space is on screen.
/// </summary>
/// <remarks>
/// Unit space is the axis' own arithmetic: a linear axis uses the value, a date axis milliseconds, a log axis
/// the exponent, a category axis the slot index. Everything above the axis - stacking, sampling, hit testing -
/// works in unit space and never in data space, which is what keeps those parts axis-agnostic.
/// </remarks>
public interface IAxis
{
    /// <summary>Which kind of <see cref="ChartValue"/> this axis understands.</summary>
    ChartValueKind ValueKind { get; }

    /// <summary>The name used in labels and legends.</summary>
    string DisplayName { get; }

    /// <summary>The stretch of unit space currently covered.</summary>
    AxisUnitRange UnitRange { get; }

    /// <summary>Turns a data value into unit space.</summary>
    /// <param name="value">The value; may be empty.</param>
    /// <param name="unit">The unit value when this returns <see langword="true"/>; otherwise 0.</param>
    /// <returns>
    /// <see langword="false"/> when the value is missing or of a kind this axis does not understand - the
    /// caller then treats the point as missing on this dimension rather than inventing a position for it.
    /// </returns>
    bool TryToUnit(ChartValue value, out double unit);

    /// <summary>Turns a unit value back into a data value; the inverse of <see cref="TryToUnit"/>.</summary>
    /// <param name="unit">The unit value.</param>
    /// <returns>The data value.</returns>
    ChartValue FromUnit(double unit);

    /// <summary>
    /// Hands the axis everything the chart wants to show on this dimension, so it can size itself before
    /// anything is drawn.
    /// </summary>
    /// <param name="bounds">The declared values, in unit space; empty when the chart has no data on this dimension.</param>
    void SetDataBounds(IReadOnlyList<BoundedValue> bounds);
}
