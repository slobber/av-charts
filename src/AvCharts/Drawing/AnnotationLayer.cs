using Avalonia.Media;
using AvCharts.Elements;
using AvCharts.Series;
using AvCharts.Transforms;

namespace AvCharts.Drawing;

/// <summary>
/// The layer that paints everything on a chart that is not a series: threshold lines, reference bands, markers
/// for events.
/// </summary>
/// <remarks>
/// The same arrangement as <see cref="SeriesLayer"/>, one step down in the paint order: the elements are models
/// rather than visuals, one visual stands in for all of them, and an element that changes asks for a repaint of
/// this layer and nothing else. Being under the series is the point - an annotation is context for the data
/// rather than a lid on it - and a chart type that wanted one over the data would add its layer above.
/// </remarks>
public class AnnotationLayer : ChartLayer
{
    private readonly List<ChartElement> _elements = [];

    /// <summary>The transform the elements read their positions from; the chart sets it during layout.</summary>
    public DataTransform? Transform { get; set; }

    /// <summary>The elements this layer paints, in the order they are drawn.</summary>
    public IReadOnlyList<ChartElement> Elements => _elements;

    /// <summary>Starts painting an element. Adding the same one twice does nothing.</summary>
    /// <param name="element">The element.</param>
    public void Add(ChartElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_elements.Contains(element))
        {
            return;
        }

        _elements.Add(element);
        element.Invalidated += OnElementInvalidated;
        InvalidateVisual();
    }

    /// <summary>Stops painting an element.</summary>
    /// <param name="element">The element.</param>
    public void Remove(ChartElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_elements.Remove(element))
        {
            element.Invalidated -= OnElementInvalidated;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void DrawLayer(DrawingContext context)
    {
        if (Transform is not { } transform)
        {
            return;
        }

        var renderContext = new SeriesRenderContext(transform, PlotBounds, RenderScaling);

        // An annotation is about the plot as well: a band a window cut through stops at the edge rather than
        // reaching into the axis' gutter.
        using (context.PushClip(PlotBounds))
        {
            // A snapshot: painting an element can finish a lazy update, and that in turn can tell every element
            // the mapping moved.
            foreach (var element in _elements.ToArray())
            {
                element.Paint(context, in renderContext);
            }
        }
    }

    private void OnElementInvalidated(object? sender, EventArgs e) => InvalidateVisual();
}
