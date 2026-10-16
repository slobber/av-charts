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
/// <para>
/// Only a transform creates one of these; an element only adds to it. Missing values must be left out rather
/// than added as zero - a gap is not a value of zero (see the null policy in the design brief).
/// </para>
/// <para>
/// Which axis a value is declared to is the element's axis for that dimension rather than the chart's: a series
/// measured against an axis of its own describes its values to that one, which is what lets two series on one
/// chart have two scales. The element says nothing about it - it goes on adding values per dimension, and the
/// transform, which is the one that knows what the element is measured against, files them accordingly.
/// </para>
/// </remarks>
public readonly struct DataDescription
{
    private readonly Dictionary<IAxis, (ChartDimension Dimension, List<BoundedValue> Bounds)> _byAxis;
    private readonly IReadOnlyDictionary<ChartDimension, IAxis> _axes;

    internal DataDescription(
        Dictionary<IAxis, (ChartDimension Dimension, List<BoundedValue> Bounds)> byAxis,
        IReadOnlyDictionary<ChartDimension, IAxis> axes)
    {
        _byAxis = byAxis;
        _axes = axes;
    }

    /// <summary>Declares one value on one dimension.</summary>
    /// <param name="dimension">The dimension the value belongs to.</param>
    /// <param name="value">The value, in unit space.</param>
    public void Add(ChartDimension dimension, BoundedValue value)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        if (!_axes.TryGetValue(dimension, out var axis))
        {
            // A dimension with no axis has nothing to describe to, so the value is not described at all.
            return;
        }

        if (!_byAxis.TryGetValue(axis, out var entry))
        {
            entry = (dimension, []);
            _byAxis[axis] = entry;
        }

        entry.Bounds.Add(value);
    }

    /// <summary>Declares one plain value on one dimension.</summary>
    /// <param name="dimension">The dimension the value belongs to.</param>
    /// <param name="value">The value, in unit space.</param>
    public void Add(ChartDimension dimension, double value) => Add(dimension, new BoundedValue(value));

    /// <summary>
    /// Declares a whole array of values on one dimension - what a series hands over, since its values are
    /// already the arrays the rest of the library reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the lowest and the highest of the values matter to an axis, so declaring a million of them costs two
    /// entries rather than a million: the array is looked at once, not handed over piece by piece. An axis that
    /// wanted the values themselves - a histogram, say - would have to be told about them some other way.
    /// </para>
    /// <para>
    /// <see cref="double.NaN"/> means the record has no value on this dimension, and is left out, like every
    /// other missing value: a gap is not a value of zero.
    /// </para>
    /// </remarks>
    /// <param name="dimension">The dimension the values belong to.</param>
    /// <param name="values">The values, in unit space.</param>
    public void Add(ChartDimension dimension, ReadOnlySpan<double> values)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        var minimum = double.NaN;
        var maximum = double.NaN;

        foreach (var value in values)
        {
            if (double.IsNaN(value))
            {
                continue;
            }

            minimum = double.IsNaN(minimum) ? value : Math.Min(minimum, value);
            maximum = double.IsNaN(maximum) ? value : Math.Max(maximum, value);
        }

        if (double.IsNaN(minimum))
        {
            return;
        }

        Add(dimension, new BoundedValue(minimum));
        Add(dimension, new BoundedValue(maximum));
    }
}
