using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// The layer that paints a chart's series.
/// </summary>
/// <remarks>
/// The series are models rather than visuals, so this one visual stands in for all of them: it hands each
/// series the same context and lets it paint. A series that changes asks for a repaint of this layer and
/// nothing else - which is the whole reason the drawing model is shaped this way.
/// </remarks>
public class SeriesLayer : ChartLayer
{
    private readonly List<Series> _series = [];

    /// <summary>The transform the series read their positions from; the chart sets it during layout.</summary>
    public DataTransform? Transform { get; set; }

    /// <summary>
    /// Where the series drawn in bands stand within their slots; the chart sets it during layout, and it is null
    /// when the chart has no such series.
    /// </summary>
    public BandLayout? Bands { get; set; }

    /// <summary>The series this layer paints, in the order they are drawn.</summary>
    public IReadOnlyList<Series> Series => _series;

    /// <summary>Starts painting a series. Adding the same one twice does nothing.</summary>
    /// <param name="series">The series.</param>
    public void Add(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (_series.Contains(series))
        {
            return;
        }

        _series.Add(series);
        series.Invalidated += OnSeriesInvalidated;
        InvalidateVisual();
    }

    /// <summary>Stops painting a series.</summary>
    /// <param name="series">The series.</param>
    public void Remove(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (_series.Remove(series))
        {
            series.Invalidated -= OnSeriesInvalidated;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void DrawLayer(DrawingContext context)
    {
        var transform = Transform;

        if (transform is null)
        {
            return;
        }

        var renderContext = new SeriesRenderContext(transform, PlotBounds, RenderScaling, Bands);

        // A snapshot: painting a series can finish a lazy update, and that in turn can tell every element the
        // mapping moved.
        foreach (var series in _series.ToArray())
        {
            series.Paint(context, renderContext);
        }
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e) => InvalidateVisual();
}
