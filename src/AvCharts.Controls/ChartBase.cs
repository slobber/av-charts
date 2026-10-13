using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using AvCharts.Charts;

namespace AvCharts.Controls;

/// <summary>
/// The base of every chart control: it owns the transform, the layers, and the layout that turns the two into a
/// rectangle of plot.
/// </summary>
/// <remarks>
/// <para>
/// The control paints almost nothing itself. It arranges a small fixed set of layers - axes, series,
/// annotations - and gets out of the way, so a repaint of one series costs one layer rather than the chart.
/// </para>
/// <para>
/// Layout is where the axis labels are measured, because the plot rectangle depends on how much room they
/// need, and how many there are depends on the plot rectangle. It is worked out in passes until it stops
/// moving, with a hard limit so that a pathological axis cannot lock the layout up.
/// </para>
/// </remarks>
public abstract class ChartBase : Control
{
    /// <summary>How many times the plot is worked out before the answer is taken as it stands.</summary>
    private const int MaximumLayoutPasses = 4;

    private readonly List<ChartLayer> _layers = [];
    private readonly SeriesLayer _seriesLayer;

    /// <summary>Creates a chart.</summary>
    protected ChartBase()
    {
        ClipToBounds = true;
        _seriesLayer = new SeriesLayer { Transform = Transform };
        AddLayer(_seriesLayer);
        Series.CollectionChanged += OnSeriesChanged;
    }

    /// <summary>The transform every layer and series reads its positions from.</summary>
    public DataTransform Transform { get; } = new();

    /// <summary>The layer that paints the series.</summary>
    public SeriesLayer SeriesLayer => _seriesLayer;

    /// <summary>The series on the chart, in drawing order. Adding one wires it up; removing one lets it go.</summary>
    public AvaloniaList<Series> Series { get; } = [];

    /// <summary>The rectangle the data is mapped into: the control's bounds less the room the axes need.</summary>
    public Rect PlotBounds { get; private set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // A chart has no natural size of its own, so it fills what it is given and picks a sensible size when
        // it is given nothing to fill.
        var size = new Size(
            double.IsInfinity(availableSize.Width) || double.IsNaN(availableSize.Width) ? 400 : availableSize.Width,
            double.IsInfinity(availableSize.Height) || double.IsNaN(availableSize.Height) ? 300 : availableSize.Height);

        foreach (var layer in _layers)
        {
            layer.Measure(size);
        }

        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounds = new Rect(finalSize);
        PlotBounds = LayoutPlot(bounds);

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

        foreach (var layer in _layers)
        {
            layer.Configure(PlotBounds, scaling);
            layer.Arrange(bounds);
        }

        return finalSize;
    }

    /// <summary>Registers a layer with the chart and adds it to the visual tree.</summary>
    /// <param name="layer">The layer.</param>
    protected void AddLayer(ChartLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        _layers.Add(layer);
        VisualChildren.Add(layer);
    }

    /// <summary>Creates the layer for one axis and registers it.</summary>
    /// <param name="renderer">The axis' renderer.</param>
    /// <returns>The layer, so the caller can configure how it paints.</returns>
    protected AxisLayer AddAxis(AxisRenderer renderer)
    {
        var layer = new AxisLayer(renderer) { Transform = Transform };
        AddLayer(layer);
        return layer;
    }

    /// <summary>
    /// Works out the plot rectangle: what is left of the control once every axis has taken the room its labels
    /// need.
    /// </summary>
    /// <param name="bounds">The control's bounds.</param>
    /// <returns>The rectangle the data is mapped into.</returns>
    protected Rect LayoutPlot(Rect bounds)
    {
        var plot = bounds;

        for (var pass = 0; ; pass++)
        {
            Transform.PlotBounds = plot;
            Transform.Update();

            var next = Deflate(bounds, plot);

            if (next == plot || pass >= MaximumLayoutPasses)
            {
                return plot;
            }

            plot = next;
        }
    }

    private Rect Deflate(Rect bounds, Rect plot)
    {
        var left = 0d;
        var bottom = 0d;

        foreach (var layer in _layers)
        {
            if (layer is not AxisLayer axis)
            {
                continue;
            }

            var gutter = axis.Renderer.MeasureGutter(Transform.GetLayout(axis.Renderer.Dimension));

            if (axis.Renderer.IsVertical)
            {
                left = Math.Max(left, gutter);
            }
            else
            {
                bottom = Math.Max(bottom, gutter);
            }
        }

        return new Rect(
            bounds.X + left,
            bounds.Y,
            Math.Max(0, bounds.Width - left),
            Math.Max(0, bounds.Height - bottom));
    }

    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (Series series in e.OldItems)
            {
                Transform.RemoveElement(series);
                _seriesLayer.Remove(series);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (Series series in e.NewItems)
            {
                Transform.AddElement(series);
                _seriesLayer.Add(series);
            }
        }

        // A new series may want more of an axis than the old ones did, which can move the plot.
        InvalidateArrange();
        _seriesLayer.InvalidateVisual();
    }
}
