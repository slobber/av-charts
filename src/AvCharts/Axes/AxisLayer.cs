using Avalonia;
using Avalonia.Media;
using AvCharts.Data;
using AvCharts.Drawing;
using AvCharts.Transforms;


namespace AvCharts.Axes;

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

    /// <summary>The brush the axis' title is drawn with; unset, no title is drawn.</summary>
    public IBrush? TitleBrush { get; set; }

    /// <summary>The pen the finer tick marks are drawn with; unset, the labelled marks' pen is used.</summary>
    /// <remarks>
    /// Its own in the theme - Flex has <c>minorTickStroke</c> for the same reason - because a finer mark is usually
    /// drawn lighter: a scale that reads as paper has one weight for the lines a reader counts by and another for
    /// the ones between them.
    /// </remarks>
    public Pen? MinorTickPen { get; set; }

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

        // The sections first, so that the grid, the axis line, the marks and the labels are all read against them:
        // a stretch of the axis is context for the scale rather than a lid on it.
        DrawSections(context, layout, plot, isVertical);

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

        // The finer marks first, so that a labelled mark - longer, and usually darker - is drawn over one that
        // happens to fall in the same place, which is what a labelled multiple of the finer step is.
        if (Renderer.MinorTickLength > 0)
        {
            var minorPen = MinorTickPen ?? tickPen;

            foreach (var position in Renderer.GetMinorTickPositions(layout, plot))
            {
                DrawMark(
                    context,
                    position,
                    plot,
                    isVertical,
                    Renderer.MinorTickLength,
                    Renderer.MinorTickPlacement,
                    minorPen);
            }
        }

        // The rows alternate along the axis, so the odd labels sit a whole row further out - the same alternation
        // the labels were thinned with, which is what lets about twice as many of them fit in the same room.
        var row = Renderer.StaggerLabels ? Renderer.MeasureLabelRow(layout) : 0;

        for (var index = 0; index < ticks.Count; index++)
        {
            DrawTick(context, ticks[index], row * (index % 2), plot, isVertical, tickPen);
        }

        DrawTitle(context, layout, plot, isVertical);
    }

    /// <summary>
    /// Draws the stretches of this axis that are marked out, behind everything else the axis paints.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="layout">What the axis worked out.</param>
    /// <param name="plot">The rectangle the data is mapped into.</param>
    /// <param name="isVertical">Whether the axis runs up the chart.</param>
    /// <remarks>
    /// A slab is the room the labels take, and no more: an axis section stands behind the labels rather than beside
    /// them, so marking out a stretch of an axis never moves the plot. A section whose ends cannot be placed - or
    /// whose ends fall outside the stretch the axis covers - draws nothing, and the labels inside it stay where they
    /// are, which is the same rule the thinning follows.
    /// </remarks>
    private void DrawSections(DrawingContext context, AxisLayout layout, Rect plot, bool isVertical)
    {
        if (Renderer.Sections.Count == 0)
        {
            return;
        }

        var near = isVertical
            ? (Renderer.Placement == AxisPlacement.End ? plot.Right : plot.Left)
            : (Renderer.Placement == AxisPlacement.End ? plot.Top : plot.Bottom);
        var room = Renderer.MeasureLabelRoom(layout);

        // Which way the gutter lies from the axis line: outwards is downwards below the plot and upwards above it,
        // and leftwards beside it and rightwards on its other side.
        var outward = isVertical
            ? (Renderer.Placement == AxisPlacement.End ? 1 : -1)
            : (Renderer.Placement == AxisPlacement.End ? -1 : 1);
        var far = near + (outward * room);

        foreach (var section in Renderer.Sections)
        {
            if (!Span(section, layout, plot, isVertical, out var first, out var second))
            {
                continue;
            }

            var low = Math.Min(first, second);
            var high = Math.Max(first, second);
            var slab = isVertical
                ? new Rect(Math.Min(near, far), low, Math.Abs(far - near), high - low)
                : new Rect(low, Math.Min(near, far), high - low, Math.Abs(far - near));

            if (section.Fill is { } fill)
            {
                context.FillRectangle(fill, slab);
            }

            if (section.Border is { } border)
            {
                foreach (var end in new[] { low, high })
                {
                    context.DrawLine(
                        border,
                        isVertical ? new Point(slab.X, end) : new Point(end, slab.Y),
                        isVertical ? new Point(slab.Right, end) : new Point(end, slab.Bottom));
                }
            }

            DrawSectionName(context, section, slab, isVertical);
        }
    }

    /// <summary>Places the two ends of one section along the axis, in pixels.</summary>
    /// <param name="section">The section.</param>
    /// <param name="layout">What the axis worked out.</param>
    /// <param name="plot">The rectangle the data is mapped into.</param>
    /// <param name="isVertical">Whether the axis runs up the chart.</param>
    /// <param name="first">Where the section starts.</param>
    /// <param name="second">Where it ends.</param>
    /// <returns><see langword="false"/> when an end is there and cannot be placed.</returns>
    private bool Span(
        AxisSection section,
        AxisLayout layout,
        Rect plot,
        bool isVertical,
        out double first,
        out double second)
    {
        second = 0;

        return EndOf(section.From, start: true, layout, plot, isVertical, out first)
            && EndOf(section.To, start: false, layout, plot, isVertical, out second);
    }

    /// <summary>Places one end of a section, with an unset end at the end of the axis.</summary>
    /// <param name="value">The reading, if there is one.</param>
    /// <param name="start">Whether this is the end the section starts at.</param>
    /// <param name="layout">What the axis worked out.</param>
    /// <param name="plot">The rectangle the data is mapped into.</param>
    /// <param name="isVertical">Whether the axis runs up the chart.</param>
    /// <param name="position">Where it sits, in pixels.</param>
    /// <returns><see langword="false"/> when the value is there and cannot be placed.</returns>
    private bool EndOf(
        ChartValue? value,
        bool start,
        AxisLayout layout,
        Rect plot,
        bool isVertical,
        out double position)
    {
        if (value is not { } reading)
        {
            position = isVertical
                ? (start ? plot.Bottom : plot.Top)
                : (start ? plot.Left : plot.Right);

            return true;
        }

        if (!Renderer.Axis.TryToUnit(reading, out var unit))
        {
            position = 0;
            return false;
        }

        var fraction = layout.UnitRange.Normalize(unit);

        position = isVertical
            ? plot.Bottom - (fraction * plot.Height)
            : plot.X + (fraction * plot.Width);

        return true;
    }

    /// <summary>Writes a section's name in the middle of its slab, reading along the axis.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="section">The section.</param>
    /// <param name="slab">The slab.</param>
    /// <param name="isVertical">Whether the axis runs up the chart.</param>
    private void DrawSectionName(DrawingContext context, AxisSection section, Rect slab, bool isVertical)
    {
        if (string.IsNullOrEmpty(section.Name))
        {
            return;
        }

        var ink = section.NameBrush ?? section.Border?.Brush ?? FillInk(section);
        var text = Renderer.CreateLabel(section.Name, ink);

        if (!isVertical)
        {
            // Along a horizontal axis the slab stands up the gutter, so its name is turned to run with it - the way
            // the axis' own title is, and for the same reason.
            var centre = new Point(slab.X + (slab.Width / 2), slab.Y + (slab.Height / 2));

            using var turned = context.PushTransform(
                Matrix.CreateTranslation(-text.Width / 2, -text.Height / 2)
                * Matrix.CreateRotation(-90 * Math.PI / 180)
                * Matrix.CreateTranslation(centre.X, centre.Y));

            context.DrawText(text, new Point());
            return;
        }

        context.DrawText(
            text,
            new Point(slab.X + ((slab.Width - text.Width) / 2), slab.Y + ((slab.Height - text.Height) / 2)));
    }

    /// <summary>The ink a section's name is written in when nobody said: its fill's colour, at full strength.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The brush, or null when there is nothing to take a colour from.</returns>
    private static IBrush? FillInk(AxisSection section) =>
        (section.Fill as ISolidColorBrush)?.Color is { } colour ? new SolidColorBrush(colour) : null;

    /// <summary>
    /// Draws the axis' title, out beyond the labels and reading along the axis.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="layout">What the axis worked out.</param>
    /// <param name="plot">The rectangle the data is mapped into.</param>
    /// <param name="isVertical">Whether the axis runs up the chart.</param>
    /// <remarks>
    /// It stands in the room the renderer measured for it rather than being fitted into what is left over: a title
    /// longer than the axis is drawn as it is and may run past the end of the plot, because the alternative - the
    /// one Flex takes - is to shrink the writing, and a chart that quietly shrinks its own lettering is a chart whose
    /// host cannot tell a title that fits from one that does not.
    /// </remarks>
    private void DrawTitle(DrawingContext context, AxisLayout layout, Rect plot, bool isVertical)
    {
        if (TitleBrush is null || Renderer.MeasureTitle() <= 0)
        {
            return;
        }

        var title = Renderer.CreateTitle(TitleBrush);
        var far = Renderer.Placement == AxisPlacement.End;
        var edge = isVertical ? (far ? plot.Right : plot.Left) : (far ? plot.Top : plot.Bottom);
        var beyond = Renderer.MeasureLabelRoom(layout) + Renderer.TitleGap;

        if (!isVertical)
        {
            // Along the middle of the axis, and standing the title's own thickness beyond the labels.
            var origin = new Point(
                plot.X + ((plot.Width - title.Width) / 2),
                far ? edge - beyond - title.Height : edge + beyond);

            context.DrawText(title, origin);
            return;
        }

        // Turned a quarter turn about its own middle, and centred along the axis: the two directions are the two
        // ways of turning the same writing, which is what the setting chooses between.
        var middle = edge + (far ? beyond + (title.Height / 2) : -beyond - (title.Height / 2));
        var centre = new Point(middle, plot.Y + (plot.Height / 2));
        var turn = Renderer.TitleDirection == AxisTitleDirection.BottomToTop ? -90 : 90;

        using var turned = context.PushTransform(
            Matrix.CreateTranslation(-title.Width / 2, -title.Height / 2)
            * Matrix.CreateRotation(turn * Math.PI / 180)
            * Matrix.CreateTranslation(centre.X, centre.Y));

        context.DrawText(title, new Point());
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
        var gap = Renderer.LabelGap;
        var far = Renderer.Placement == AxisPlacement.End;
        var edge = isVertical ? (far ? plot.Right : plot.Left) : (far ? plot.Top : plot.Bottom);

        DrawMark(context, tick.Position, plot, isVertical, Renderer.TickLength, Renderer.TickPlacement, pen);

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

    /// <summary>Draws one mark across the axis line, at a position along the plot.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="position">Where the mark stands along the plot, in pixels.</param>
    /// <param name="plot">The rectangle the data is mapped into.</param>
    /// <param name="isVertical">Whether the axis runs up the chart.</param>
    /// <param name="length">How long the mark is, in logical pixels.</param>
    /// <param name="placement">Which side of the axis line it is drawn on.</param>
    /// <param name="pen">The pen, or null for a mark that is not drawn at all.</param>
    /// <remarks>
    /// The labelled marks and the finer ones are the same thing with different settings, which is why they are the
    /// same code: Flex has a length, a placement and a stroke for each, and gives them the same four placements.
    /// </remarks>
    private void DrawMark(
        DrawingContext context,
        double position,
        Rect plot,
        bool isVertical,
        double length,
        AxisTickPlacement placement,
        Pen? pen)
    {
        var far = Renderer.Placement == AxisPlacement.End;
        var edge = isVertical ? (far ? plot.Right : plot.Left) : (far ? plot.Top : plot.Bottom);

        // How far the mark reaches out of the plot and how far into it: outward for a mark drawn outside, inward
        // for one drawn inside, and half each way for one drawn across - Flex's four placements, with "none" the
        // one that draws nothing.
        var (reach, into) = placement switch
        {
            AxisTickPlacement.Outside => (length, 0d),
            AxisTickPlacement.Inside => (0d, length),
            AxisTickPlacement.Cross => (length / 2, length / 2),
            _ => (0d, 0d),
        };

        if (pen is null || !(reach > 0 || into > 0))
        {
            return;
        }

        var outside = isVertical
            ? new Point(far ? edge + reach : edge - reach, position)
            : new Point(position, far ? edge - reach : edge + reach);
        var inside = isVertical
            ? new Point(far ? edge - into : edge + into, position)
            : new Point(position, far ? edge + into : edge - into);

        context.DrawLine(pen, outside, inside);
    }
}
