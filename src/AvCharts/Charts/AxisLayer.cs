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
/// Pens and brushes are properties rather than lookups: a layer is told what to paint with, and the chart
/// control that owns it hands down the inks its theme worked out. That keeps this half of the library free of
/// any idea of where colours come from.
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
    public bool ShowGridLines
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateVisual();
        }
    } = true;

    /// <summary>The pen for the grid lines at the labelled intervals.</summary>
    public Pen? GridLinePen { get; set; }

    /// <summary>The pen for the finer grid lines, if any are wanted.</summary>
    public Pen? MinorGridLinePen { get; set; }

    /// <summary>The pen for the axis line and its tick marks.</summary>
    public Pen? AxisLinePen { get; set; }

    /// <summary>The pen for the tick marks; unset, they are drawn with the pen of the axis line.</summary>
    public Pen? TickPen { get; set; }

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

        // The axis' own layout rather than the dimension's: a chart can have a second axis on a dimension, and
        // each of them was resolved against the values of the series measured against it.
        var layout = transform.GetLayout(Renderer.Axis);
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
            // The near edge or the far one, which is the whole of what placement means for the line itself.
            var far = Renderer.Placement == AxisPlacement.End;

            context.DrawLine(
                axisPen,
                isVertical
                    ? new Point(far ? plot.Right : plot.Left, plot.Top)
                    : new Point(plot.Left, far ? plot.Top : plot.Bottom),
                isVertical
                    ? new Point(far ? plot.Right : plot.Left, plot.Bottom)
                    : new Point(plot.Right, far ? plot.Top : plot.Bottom));
        }

        var tickPen = TickPen ?? axisPen;
        var ticks = Renderer.GetTicks(layout, plot);

        // The rows alternate along the axis, so the odd labels sit a whole row further out - the same alternation
        // the labels were thinned with, which is what lets about twice as many of them fit in the same room.
        var row = Renderer.StaggerLabels ? Renderer.MeasureLabelRow(layout) : 0;

        for (var index = 0; index < ticks.Count; index++)
        {
            DrawTick(context, ticks[index], row * (index % 2), plot, isVertical, tickPen);
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

    private void DrawTick(
        DrawingContext context,
        AxisTick tick,
        double stagger,
        Rect plot,
        bool isVertical,
        Pen? pen)
    {
        var length = Renderer.TickLength;
        var gap = Renderer.LabelGap;
        var far = Renderer.Placement == AxisPlacement.End;
        var edge = isVertical ? (far ? plot.Right : plot.Left) : (far ? plot.Top : plot.Bottom);

        // How far the mark reaches out of the plot and how far into it: outward for a mark drawn outside, inward
        // for one drawn inside, and half each way for one drawn across - Flex's four placements, with "none" the
        // one that draws nothing.
        var (reach, into) = Renderer.TickPlacement switch
        {
            AxisTickPlacement.Outside => (length, 0d),
            AxisTickPlacement.Inside => (0d, length),
            AxisTickPlacement.Cross => (length / 2, length / 2),
            _ => (0d, 0d),
        };

        if (pen is not null && (reach > 0 || into > 0))
        {
            var outside = isVertical
                ? new Point(far ? edge + reach : edge - reach, tick.Position)
                : new Point(tick.Position, far ? edge - reach : edge + reach);
            var inside = isVertical
                ? new Point(far ? edge - into : edge + into, tick.Position)
                : new Point(tick.Position, far ? edge + into : edge - into);

            context.DrawLine(pen, outside, inside);
        }

        if (!Renderer.ShowLabels || LabelBrush is null)
        {
            return;
        }

        var label = Renderer.CreateLabel(tick.Label, LabelBrush);
        var offset = Renderer.OutwardReach + gap + stagger;

        if (Renderer.LabelRotation == 0)
        {
            // Upright, which is the arithmetic this axis has always used - kept apart from the turned case so that
            // a chart that asks for no rotation is drawn the same way to the pixel.
            var origin = isVertical
                ? new Point(
                    far ? edge + offset : edge - offset - label.Width,
                    tick.Position - (label.Height / 2))
                : new Point(
                    tick.Position - (label.Width / 2),
                    far ? edge - offset - label.Height : edge + offset);

            context.DrawText(label, origin);
            return;
        }

        // How far past the edge the middle of the turned label stands: its near side sits on the row it was given,
        // and half of the box the axis measured it as reaches beyond that.
        var middle = offset + (Renderer.MeasureLabelBox(new Size(label.Width, label.Height)).Across / 2);

        // Turned about its own middle, so that what was measured is what is drawn: a vertical axis reads against
        // the plot from whichever edge it is on, and a horizontal one reads centred above or below its tick.
        var centre = isVertical
            ? new Point(far ? edge + middle : edge - middle, tick.Position)
            : new Point(tick.Position, far ? edge - middle : edge + middle);

        using var turned = context.PushTransform(
            Matrix.CreateTranslation(-label.Width / 2, -label.Height / 2)
            * Matrix.CreateRotation(-Renderer.LabelRotation * Math.PI / 180)
            * Matrix.CreateTranslation(centre.X, centre.Y));

        context.DrawText(label, new Point());
    }
}
