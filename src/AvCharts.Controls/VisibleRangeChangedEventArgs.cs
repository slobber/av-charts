using AvCharts.Charts;

namespace AvCharts.Controls;

/// <summary>What a chart's window onto a dimension has become.</summary>
/// <remarks>
/// The event a host links two charts with: one chart says what it now shows, the other is told to show the same.
/// The range is in unit space - the axis' own arithmetic - so a pair of charts can be linked without either of
/// them knowing what the other is measuring.
/// </remarks>
public sealed class VisibleRangeChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="dimension">The dimension whose window moved.</param>
    /// <param name="range">What that dimension now shows, in unit space.</param>
    public VisibleRangeChangedEventArgs(ChartDimension dimension, AxisUnitRange range)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        Dimension = dimension;
        Range = range;
    }

    /// <summary>The dimension whose window moved.</summary>
    public ChartDimension Dimension { get; }

    /// <summary>What that dimension now shows, in unit space.</summary>
    public AxisUnitRange Range { get; }
}
