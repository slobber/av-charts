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
/// Everything an axis worked out for one chart: the stretch of unit space on screen and where its grid lines
/// fall.
/// </summary>
/// <param name="UnitRange">The stretch of unit space covered.</param>
/// <param name="MajorInterval">The distance between labelled lines, in unit space.</param>
/// <param name="MinorInterval">The distance between the finer lines, in unit space.</param>
/// <param name="MajorTicks">The labelled positions, in unit space, ascending.</param>
/// <param name="MinorTicks">The finer positions, in unit space, ascending.</param>
public sealed record AxisLayout(
    AxisUnitRange UnitRange,
    double MajorInterval,
    double MinorInterval,
    IReadOnlyList<double> MajorTicks,
    IReadOnlyList<double> MinorTicks)
{
    /// <summary>An axis that worked nothing out: no data, no settings.</summary>
    public static AxisLayout Empty { get; } = new(AxisUnitRange.Empty, 0, 0, [], []);
}

/// <summary>
/// What a transform needs from an axis: how to turn the values of one dimension into a single monotonic
/// number, and - given the data and the room available - how much of that number space to put on screen.
/// </summary>
/// <remarks>
/// <para>
/// Unit space is the axis' own arithmetic: a linear axis uses the value, a date axis milliseconds, a log axis
/// the exponent, a category axis the slot index. Everything above the axis - stacking, sampling, hit testing -
/// works in unit space and never in data space, which is what keeps those parts axis-agnostic.
/// </para>
/// <para>
/// An axis holds settings, never results: <see cref="Resolve"/> is a pure function of the data and the room.
/// That is what lets one axis instance drive several charts - a dashboard of charts sharing a time window -
/// without the widest one overwriting what the others see.
/// </para>
/// </remarks>
public interface IAxis
{
    /// <summary>Which kind of <see cref="ChartValue"/> this axis understands.</summary>
    ChartValueKind ValueKind { get; }

    /// <summary>The name used in labels and legends.</summary>
    string DisplayName { get; }

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

    /// <summary>Writes a unit value the way a label should read it.</summary>
    /// <param name="unit">The value, in unit space.</param>
    /// <returns>The text.</returns>
    string FormatValue(double unit);

    /// <summary>Works out what this axis covers, given the data and the room it has.</summary>
    /// <param name="bounds">The declared values, in unit space; empty when the chart has no data on this dimension.</param>
    /// <param name="lengthInPixels">How long this dimension is on screen, in pixels.</param>
    /// <returns>The layout; <see cref="AxisLayout.Empty"/> when there is nothing to cover.</returns>
    AxisLayout Resolve(IReadOnlyList<BoundedValue> bounds, double lengthInPixels);
}
