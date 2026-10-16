using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;






namespace AvCharts.Drawing;

/// <summary>
/// One layer of a chart: a visual that paints a slice of it, keeps what it painted, and can be invalidated on
/// its own.
/// </summary>
/// <remarks>
/// <para>
/// A chart owns a fixed, small set of these - grid lines, series, annotations, axes - rather than one visual
/// per data point. Avalonia keeps the drawing of a visual until it is invalidated, so a still chart costs
/// nothing to keep on screen, and hovering one marker re-records one layer rather than the whole chart.
/// </para>
/// <para>
/// This lives in the model assembly on purpose: <see cref="Visual"/> and everything it draws with are
/// Avalonia's base half, so laying out a chart and painting it does not need the control stack.
/// </para>
/// </remarks>
public abstract class ChartLayer : Layoutable
{
    /// <summary>Creates a layer that clips to its own bounds.</summary>
    protected ChartLayer() => ClipToBounds = true;

    /// <summary>The rectangle the data is mapped into, in this layer's coordinates.</summary>
    public Rect PlotBounds { get; private set; }

    /// <summary>How many device pixels one logical pixel covers; 1 when nothing said otherwise.</summary>
    public double RenderScaling { get; private set; } = 1;

    /// <summary>
    /// Tells the layer where the plot is and how finely it is being drawn, and asks for a repaint when either
    /// changed. Nothing is repainted when neither did.
    /// </summary>
    /// <param name="plotBounds">The rectangle the data is mapped into, in this layer's coordinates.</param>
    /// <param name="renderScaling">How many device pixels one logical pixel covers.</param>
    public void Configure(Rect plotBounds, double renderScaling)
    {
        var scaling = renderScaling > 0 ? renderScaling : 1;

        if (PlotBounds == plotBounds && RenderScaling.Equals(scaling))
        {
            return;
        }

        PlotBounds = plotBounds;
        RenderScaling = scaling;
        InvalidateVisual();
    }

    /// <summary>Paints this layer. Called by Avalonia when the layer is invalidated - and only then.</summary>
    /// <param name="context">The drawing context.</param>
    public sealed override void Render(DrawingContext context) => DrawLayer(context);

    /// <summary>Paints this layer.</summary>
    /// <param name="context">The drawing context.</param>
    protected abstract void DrawLayer(DrawingContext context);
}
