namespace AvCharts.Charts;

/// <summary>
/// Something a transform knows about: it declares what it wants to show, and it is told when the mapping
/// moves.
/// </summary>
/// <remarks>
/// Series are the common case, but an annotation band, a data canvas or a custom overlay implement this too -
/// the transform does not care which, only that the element can describe its data and can be told to redraw.
/// </remarks>
public interface IChartElement
{
    /// <summary>
    /// Declares the values this element needs on each dimension, in unit space. Missing values are left out.
    /// </summary>
    /// <param name="description">The description to add to.</param>
    void DescribeData(DataDescription description);

    /// <summary>Called when the mapping changed: positions are stale and the element should recompute them.</summary>
    void MappingChanged();
}
