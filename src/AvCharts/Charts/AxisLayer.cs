using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// The layer that paints one dimension's axis: the grid across the plot, the axis line, the tick marks and the
/// labels.
/// </summary>
/// <remarks>
/// <para>
/// The grid follows every interval the axis worked out, while the marks and labels follow the ones
/// <see cref="AxisRenderer"/> kept: a dense grid under sparse labels is easier to read than both together, and
/// it is what the fixed pixel pitch in the design brief is for.
/// </para>
/// <para>
/// Pens and brushes are properties rather than theme lookups for now; the tokens they will read from arrive in
/// the theming step.
/// </para>
/// </remarks>
public class AxisLayer : ChartLayer
{
    /// <summary>Creates a layer for one axis.</summary>
    /// <param name="renderer">The renderer that turns the axis' arithmetic into marks and labels.</param>
    public AxisLayer(AxisRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Renderer = renderer;
    }

    /// <summary>The renderer this layer draws.</summary>
    public AxisRenderer Renderer { get; }

    /// <summary>The transform the axis belongs to; the chart sets it during layout.</summary>
    public DataTransform? Transform { get; set; }

    /// <summary>Whether to draw the grid across the plot at all.</summary>
    public bool ShowGridLines { get; set; } = true;

    /// <summary>The pen for the grid lines at the labelled intervals.</summary>
    public Pen? GridLinePen { get; set; }

    /// <summary>The pen for the finer grid lines, if any are wanted.</summary>
    public Pen? MinorGridLinePen { get; set; }

    /// <summary>The pen for the axis line and its tick marks.</summary>
    public Pen? AxisLinePen { get; set; }

    /// <summary>The brush the labels are drawn with.</summary>
    public IBrush? LabelBrush { get; set; }

    /// <inheritdoc />
    protected override void DrawLayer(DrawingContext context)
    {
        var transform = Transform;

        if (transform is null)
        {
            return;
        }

        var layout = transform.GetLayout(Renderer.Dimension);
        var plot = PlotBounds;
        var isVertical = Renderer.IsVertical;

        if (ShowGridLines)
        {
            DrawGrid(context, layout, plot, isVertical, MinorGridLinePen);
            DrawGrid(context, layout, plot, isVertical, GridLinePen, layout.MajorTicks);
        }

        var axisPen = AxisLinePen;

        if (axisPen is not null)
        {
            context.DrawLine(
                axisPen,
                isVertical ? new Point(plot.Left, plot.Top) : new Point(plot.Left, plot.Bottom),
                isVertical ? new Point(plot.Left, plot.Bottom) : new Point(plot.Right, plot.Bottom));
        }

        foreach (var tick in Renderer.GetTicks(layout, plot))
        {
            DrawTick(context, tick, plot, isVertical, axisPen);
        }
    }

    private void DrawGrid(
        DrawingContext context,
        AxisLayout layout,
        Rect plot,
        bool isVertical,
        Pen? pen,
        IReadOnlyList<double>? ticks = null)
    {
        if (pen is null)
        {
            return;
        }

        foreach (var unit in ticks ?? layout.MinorTicks)
        {
            var fraction = layout.UnitRange.Normalize(unit);

            if (isVertical)
            {
                var y = plot.Bottom - (fraction * plot.Height);
                context.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
            }
            else
            {
                var x = plot.X + (fraction * plot.Width);
                context.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            }
        }
    }

    private void DrawTick(DrawingContext context, AxisTick tick, Rect plot, bool isVertical, Pen? pen)
    {
        var length = Renderer.TickLength;
        var gap = Renderer.LabelGap;

        if (pen is not null)
        {
            context.DrawLine(
                pen,
                isVertical ? new Point(plot.Left - length, tick.Position) : new Point(tick.Position, plot.Bottom),
                isVertical ? new Point(plot.Left, tick.Position) : new Point(tick.Position, plot.Bottom + length));
        }

        if (!Renderer.ShowLabels || LabelBrush is null)
        {
            return;
        }

        var label = Renderer.CreateLabel(tick.Label, LabelBrush);

        // A vertical axis reads right to left against the plot; a horizontal one reads centred under its tick.
        var origin = isVertical
            ? new Point(plot.Left - length - gap - label.Width, tick.Position - (label.Height / 2))
            : new Point(tick.Position - (label.Width / 2), plot.Bottom + length + gap);

        context.DrawText(label, origin);
    }
}
