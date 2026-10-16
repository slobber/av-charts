namespace AvCharts.Charts;

/// <summary>
/// One record of one series, seen as a data point rather than as ink: what a hit test finds, what a data tip
/// names and what a legend or an export walks.
/// </summary>
/// <remarks>
/// <para>
/// A view and not a copy. It is a pair of a series and a position in it, and every value is read from the
/// series when it is asked for, so walking a million points does not build a million of these - which is what
/// keeps a chart of a million points from needing a second copy of them. The design brief calls that lazy
/// materialisation, and it is why <see cref="Series.Items"/> is a view rather than a list.
/// </para>
/// <para>
/// A record with no value on a dimension reads as <see cref="ChartValue.Empty"/> there, which is how "there was
/// no reading" stays different from "the reading was zero".
/// </para>
/// </remarks>
public readonly struct ChartItem
{
    /// <summary>Creates a view of one record.</summary>
    /// <param name="series">The series the record belongs to.</param>
    /// <param name="index">The record's position in that series.</param>
    internal ChartItem(Series series, int index)
    {
        Series = series;
        Index = index;
    }

    /// <summary>The series the record belongs to.</summary>
    public Series Series { get; }

    /// <summary>The record's position in its series, counting the records the series kept.</summary>
    public int Index { get; }

    /// <summary>The caller's object, when the series can still find it; null for a series without a source.</summary>
    public object? Item => Series.SourceItemAt(Index);

    /// <summary>The horizontal value, in data space; empty when the record has none.</summary>
    public ChartValue X => Value(ChartDimension.X);

    /// <summary>The vertical value, in data space; empty when the record has none.</summary>
    public ChartValue Y => Value(ChartDimension.Y);

    /// <summary>The position of the record on the chart, when it has one.</summary>
    /// <returns>The point, or null when the record cannot be placed or the series is not in a chart.</returns>
    public Avalonia.Point? Position => Series.PositionOf(Index);

    /// <inheritdoc />
    public override string ToString() => $"{Series.DisplayName}[{Index}]";

    private ChartValue Value(ChartDimension dimension)
    {
        var unit = Series.UnitAt(dimension, Index);

        return double.IsNaN(unit) || Series.AxisFor(dimension) is not { } axis
            ? ChartValue.Empty
            : axis.FromUnit(unit);
    }
}
