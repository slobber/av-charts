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

    private void SetAxis(AxisLayer layer, ChartDimension dimension, IAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        layer.Renderer.Axis = axis;
        Transform.SetAxis(dimension, axis);
        InvalidateArrange();
    }
}
