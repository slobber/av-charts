using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using AvCharts.Data;
using AvCharts.Series;
using AvCharts.Transforms;

namespace AvCharts.Elements;

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
/// an element is a model rather than a control.
/// </para>
/// <para>
/// An element that has to be a real control - a button, something that takes the keyboard, a shape only a theme
/// knows how to draw - says so through <see cref="Visual"/> and <see cref="VisualAnchor"/>, and the chart hangs
/// that one control on itself, over the layers, centred on the anchor. A handful of those, as the design brief
/// puts it: a control per reading is a chart that has stopped being a drawing, and a hundred thousand of them is
/// a page that will not open.
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

    /// <summary>
    /// The Visual this element carries, or null for an element that paints itself and nothing more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A control the element made and owns: the chart puts it in its own tree, over the layers, measures it and
    /// stands it where <see cref="VisualAnchor"/> says - and takes it away again when the element leaves the
    /// chart. It is re-read on every layout pass, so an element that answers a different one dresses the chart
    /// differently rather than waiting to be taken off and put back.
    /// </para>
    /// <para>
    /// What this buys is everything a drawn element cannot do: buttons, focus, real text layout, a theme of the
    /// host's own. What it costs is a control in the visual tree, which is why the design brief asks for a few.
    /// </para>
    /// <para>
    /// A <see cref="Layoutable"/> rather than a <see cref="Visual"/>: a chart is handed paint and has to place
    /// what it is given, and a bare Visual has no size to place. Every control is one.
    /// </para>
    /// </remarks>
    public virtual Layoutable? Visual => null;

    /// <summary>
    /// Where the carried Visual stands, in the chart's own coordinates: the point its middle is on.
    /// </summary>
    /// <returns>The point, or null when there is nowhere to put it and the control is not shown.</returns>
    /// <remarks>
    /// The element works this out for itself, usually in <see cref="MappingChanged"/> - it has the transform, and
    /// turning a value into a position is what the transform is for - and answers the same point until the
    /// mapping moves again. The centre rather than a corner, because what an element carries is a thing standing
    /// on a reading: which way its edges fall is nobody's business but its own.
    /// </remarks>
    public virtual Point? VisualAnchor => null;

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
