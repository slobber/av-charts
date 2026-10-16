using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Something drawn on a chart that is not a series: a threshold line, a reference band, a marker for an event.
/// </summary>
/// <remarks>
/// <para>
/// The modern half of what Flex calls a <c>CartesianDataCanvas</c>, without the canvas: an element declares what
/// the axes have to cover - usually nothing, which is the point of most annotations - and paints itself. It is
/// put on a chart by adding it to the chart's elements, which keeps it in a layer under the data.
/// </para>
/// <para>
/// Experimental in v1, as the design brief has it: the shape may change. What is settled is the division of
/// labour - <see cref="DescribeData"/> for what the axes need to know, <see cref="Draw"/> for the ink - and that
/// an element is a model rather than a control. An annotation that has to be a real control, with buttons and
/// focus on it, belongs in the hosting page's own overlay: a chart hands out paint, not layout.
/// </para>
/// </remarks>
public abstract class ChartElement : IChartElement
{
    /// <summary>Raised when the element changed and whatever draws it should repaint.</summary>
    public event EventHandler? Invalidated;

    /// <summary>The transform this element is registered with, set when it is added to one.</summary>
    public DataTransform? Transform { get; internal set; }

    /// <summary>Paints the element. Called by the layer it is registered with, inside that layer's render pass.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="renderContext">Where the plot is and how to map values into it.</param>
    public void Paint(DrawingContext context, in SeriesRenderContext renderContext) => Draw(context, in renderContext);

    /// <summary>Paints the element.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="renderContext">Where the plot is and how to map values into it.</param>
    protected abstract void Draw(DrawingContext context, in SeriesRenderContext renderContext);

    /// <summary>Declares what the axes have to cover for this element; nothing, by default.</summary>
    /// <param name="description">The description to add to.</param>
    /// <remarks>
    /// Empty by default, because an annotation that stretched the axes would defeat the point of most
    /// annotations: a threshold line at a hundred is usually drawn on a chart of readings between zero and ten
    /// precisely because it is out of reach. An element that does want room made for it - a band the data is
    /// expected to stay inside - overrides this and says so.
    /// </remarks>
    protected virtual void DescribeData(DataDescription description)
    {
    }

    /// <summary>Called when the mapping moved: positions are stale and the element should recompute them.</summary>
    protected virtual void MappingChanged()
    {
    }

    /// <summary>Asks whatever draws this element to repaint.</summary>
    protected void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);

    void IChartElement.DescribeData(DataDescription description) => DescribeData(description);

    void IChartElement.MappingChanged() => MappingChanged();
}
