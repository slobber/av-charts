using AvCharts.Charts;

namespace AvCharts.Controls;

/// <summary>
/// A chart of two axes: values across and values up.
/// </summary>
/// <remarks>
/// The chart type classes - <c>LineChart</c>, <c>ColumnChart</c> and the rest - are conveniences that preset
/// what this one does; any Cartesian series can be put on any of them, which is how a column chart with a line
/// over it works.
/// </remarks>
public class CartesianChart : ChartBase
{
    private readonly AxisLayer _horizontalLayer;
    private readonly AxisLayer _verticalLayer;
    private SeriesLayout _seriesLayout = SeriesLayout.Overlay;

    /// <summary>Creates a chart with a linear axis on each dimension.</summary>
    public CartesianChart()
    {
        _horizontalLayer = AddAxis(new AxisRenderer(new LinearAxis(), ChartDimension.X));
        _verticalLayer = AddAxis(new AxisRenderer(new LinearAxis(), ChartDimension.Y));

        Transform.SetAxis(ChartDimension.X, _horizontalLayer.Renderer.Axis);
        Transform.SetAxis(ChartDimension.Y, _verticalLayer.Renderer.Axis);
    }

    /// <summary>The renderer of the horizontal axis: where its labels, marks and grid are configured.</summary>
    public AxisRenderer HorizontalRenderer => _horizontalLayer.Renderer;

    /// <summary>The renderer of the vertical axis.</summary>
    public AxisRenderer VerticalRenderer => _verticalLayer.Renderer;

    /// <summary>The layer that paints the horizontal axis: where its pens and label brush live.</summary>
    public AxisLayer HorizontalLayer => _horizontalLayer;

    /// <summary>The layer that paints the vertical axis.</summary>
    public AxisLayer VerticalLayer => _verticalLayer;

    /// <summary>The axis across the chart.</summary>
    public IAxis HorizontalAxis
    {
        get => _horizontalLayer.Renderer.Axis;
        set => SetAxis(_horizontalLayer, ChartDimension.X, value);
    }

    /// <summary>The axis up the chart.</summary>
    public IAxis VerticalAxis
    {
        get => _verticalLayer.Renderer.Axis;
        set => SetAxis(_verticalLayer, ChartDimension.Y, value);
    }

    /// <summary>
    /// How the series laid out in bands - columns, bars - share the space of a slot.
    /// </summary>
    /// <remarks>
    /// Overlaid by default, which is what Flex does and what a column chart with a line over it wants: the line
    /// takes no room from the columns, and the columns stand in the whole slot. Two series of columns side by
    /// side want <see cref="SeriesLayout.Cluster"/>, and accumulated columns want
    /// <see cref="SeriesLayout.Stacked"/>.
    /// </remarks>
    public SeriesLayout SeriesLayout
    {
        get => _seriesLayout;
        set
        {
            if (_seriesLayout == value)
            {
                return;
            }

            _seriesLayout = value;
            InvalidateArrange();
            SeriesLayer.InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void PrepareLayout()
    {
        // The series that stand in bands are the chart's business rather than any one series': a series cannot
        // know how many others share its slot, and the answer changes when one is added or removed.
        var bands = new BandLayout(_seriesLayout);
        var stacked = _seriesLayout == SeriesLayout.Stacked;
        var totals = new StackTotals();

        foreach (var series in Series)
        {
            if (series is IColumn)
            {
                bands.Add(series);
            }

            if (series is IStackable stackable)
            {
                // Stacked, the series fold onto one set of totals in the order they are declared. Any other way,
                // each gets totals of its own, so every column stands on the base and none of them accumulates -
                // the same code, told a different story about what is underneath it.
                stackable.Stack(stacked ? totals : new StackTotals());
            }
        }

        SeriesLayer.Bands = bands.Count > 0 ? bands : null;
    }

    private void SetAxis(AxisLayer layer, ChartDimension dimension, IAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        layer.Renderer.Axis = axis;
        Transform.SetAxis(dimension, axis);
        InvalidateArrange();
    }
}
