namespace AvCharts.Charts;

/// <summary>
/// One value a chart element needs on one dimension, together with the room it needs around that value
/// (a marker that sticks out, a label under a column).
/// </summary>
/// <param name="Value">The value itself, in unit space.</param>
/// <param name="LowerMargin">How far below <paramref name="Value"/> the element draws, in unit space.</param>
/// <param name="UpperMargin">How far above <paramref name="Value"/> the element draws, in unit space.</param>
public readonly record struct BoundedValue(double Value, double LowerMargin = 0, double UpperMargin = 0)
{
    /// <summary>The lowest unit value this bound covers.</summary>
    public double Minimum => Value - LowerMargin;

    /// <summary>The highest unit value this bound covers.</summary>
    public double Maximum => Value + UpperMargin;
}

/// <summary>
/// What a chart element hands the transform in <see cref="IChartElement.DescribeData"/>: the values it wants
/// to show, per dimension, so the axes can size themselves before anything is drawn.
/// </summary>
/// <remarks>
/// Only a transform creates one of these; an element only adds to it. Missing values must be left out rather
/// than added as zero - a gap is not a value of zero (see the null policy in the design brief).
/// </remarks>
public readonly struct DataDescription
{
    private readonly Dictionary<ChartDimension, List<BoundedValue>> _byDimension;

    internal DataDescription(Dictionary<ChartDimension, List<BoundedValue>> byDimension) =>
        _byDimension = byDimension;

    /// <summary>Declares one value on one dimension.</summary>
    /// <param name="dimension">The dimension the value belongs to.</param>
    /// <param name="value">The value, in unit space.</param>
    public void Add(ChartDimension dimension, BoundedValue value)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        if (!_byDimension.TryGetValue(dimension, out var values))
        {
            values = [];
            _byDimension[dimension] = values;
        }

        values.Add(value);
    }

    /// <summary>Declares one plain value on one dimension.</summary>
    /// <param name="dimension">The dimension the value belongs to.</param>
    /// <param name="value">The value, in unit space.</param>
    public void Add(ChartDimension dimension, double value) => Add(dimension, new BoundedValue(value));
}
