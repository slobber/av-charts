using System.Globalization;
using Avalonia;
using Avalonia.Media;


using AvCharts.Data;



namespace AvCharts.Drawing;

/// <summary>
/// The layer that draws the crosshair: a line each way through the point it is on, and the value written
/// beside each axis.
/// </summary>
/// <remarks>
/// <para>
/// A layer of its own rather than something the series layer draws, for the reason every layer exists: a pointer
/// moving repaints this and nothing else. It sits above the series, so the lines read over the ink rather than
/// under it.
/// </para>
/// <para>
/// What it is on is the nearest <em>valid</em> point rather than where the pointer happens to be, which is the
/// rule the design brief settles on: a crosshair stopped on a gap would be pointing at a reading that does not
/// exist. The readouts take their text from the axis of the series the point belongs to, so a second scale is
/// read out in its own numbers.
/// </para>
/// </remarks>
public class CrosshairLayer : ChartLayer
{
    /// <summary>The dotted line of the crosshair; unset, no line is drawn.</summary>
    public Pen? LinePen { get; set; }

    /// <summary>The fill of the chips that carry the readouts; unset, no readout is drawn.</summary>
    public IBrush? LabelBackground { get; set; }

    /// <summary>The colour the readouts are written in.</summary>
    public IBrush? LabelForeground { get; set; }

    /// <summary>The size of the readouts, in points.</summary>
    public double FontSize { get; set; } = 11;

    /// <summary>The face the readouts are set in.</summary>
    public Typeface Typeface { get; set; } = new(FontFamily.Default);

    /// <summary>The point the crosshair is on, or null when it is not showing.</summary>
    /// <remarks>
    /// A hit rather than a position, so the layer knows which series it belongs to and can read that series' own
    /// axis out; a host gets the same thing from the chart's own crosshair property.
    /// </remarks>
    public HitData? Hit
    {
        get;
        set
        {
            if (Nullable.Equals(field, value))
            {
                return;
            }

            field = value;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void DrawLayer(DrawingContext context)
    {
        if (Hit is not { } hit || hit.Position is not { } at)
        {
            return;
        }

        var plot = PlotBounds;
        var position = Snap(in at);

        if (LinePen is { } line)
        {
            context.DrawLine(line, new Point(position.X, plot.Top), new Point(position.X, plot.Bottom));
            context.DrawLine(line, new Point(plot.Left, position.Y), new Point(plot.Right, position.Y));
        }

        if (LabelBackground is { } background && LabelForeground is { } foreground)
        {
            DrawReadout(context, background, foreground, Readout(hit, ChartDimension.X), position.X, plot.Bottom, vertical: true);
            DrawReadout(context, background, foreground, Readout(hit, ChartDimension.Y), position.Y, plot.Left, vertical: false);
        }
    }

    /// <summary>Draws one readout chip, centred on its place along the axis it belongs to.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="background">The fill of the chip.</param>
    /// <param name="foreground">The colour the text is written in.</param>
    /// <param name="text">The text, already written by the axis it came from.</param>
    /// <param name="along">Where along that axis the chip belongs, in this layer's coordinates.</param>
    /// <param name="across">The edge of the plot that axis is drawn against.</param>
    /// <param name="vertical">Whether the chip hangs below the point, rather than beside it.</param>
    private void DrawReadout(
        DrawingContext context,
        IBrush background,
        IBrush foreground,
        string text,
        double along,
        double across,
        bool vertical)
    {
        var label = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface,
            FontSize,
            foreground);

        const double Padding = 3;
        const double Gap = 1;
        var width = label.Width + (Padding * 2);
        var height = label.Height + (Padding * 2);

        // Beside the axis when there is room for the reading in the gutter, and inside the plot when there is not:
        // a readout that ran off the edge of the chart, or over the labels of the axis it is reading, would be
        // worth less than the space it took. Either way the chip is kept on the chart.
        var room = vertical ? Bounds.Bottom - across : across;
        var beside = room >= (vertical ? height : width) + Gap;
        var x = vertical ? along - (width / 2) : beside ? across - width - Gap : across + Gap;
        var y = vertical ? beside ? across + Gap : across - height - Gap : along - (height / 2);
        var chip = new Rect(
            Math.Clamp(x, 0, Math.Max(0, Bounds.Width - width)),
            Math.Clamp(y, 0, Math.Max(0, Bounds.Height - height)),
            width,
            height);

        context.FillRectangle(background, Snap(chip));
        context.DrawText(label, new Point(chip.X + Padding, chip.Y + Padding));
    }

    /// <summary>One readout's text: the value as the axis of the series that was hit writes it.</summary>
    /// <param name="hit">The point the crosshair is on.</param>
    /// <param name="dimension">Which of the two values to write.</param>
    /// <returns>The text, or a dash when there is no reading to write.</returns>
    private static string Readout(HitData hit, ChartDimension dimension)
    {
        var value = dimension == ChartDimension.X ? hit.X : hit.Y;

        if (value.IsEmpty || hit.Element.AxisFor(dimension) is not { } axis)
        {
            return "—";
        }

        return axis.TryToUnit(value, out var unit) ? axis.FormatValue(unit) : "—";
    }

    /// <summary>Lines up a point with the device pixel grid, so the hairline is a hairline.</summary>
    /// <param name="point">The point to line up.</param>
    /// <returns>The point, on the nearest device pixel boundary.</returns>
    private Point Snap(in Point point) =>
        new(Math.Round(point.X * RenderScaling) / RenderScaling, Math.Round(point.Y * RenderScaling) / RenderScaling);

    /// <summary>Lines up a rectangle with the device pixel grid, so the chip's fill covers whole pixels.</summary>
    /// <param name="rect">The rectangle to line up.</param>
    /// <returns>The rectangle, on the nearest device pixel boundaries.</returns>
    private Rect Snap(in Rect rect) => new(
        Math.Round(rect.X * RenderScaling) / RenderScaling,
        Math.Round(rect.Y * RenderScaling) / RenderScaling,
        Math.Round(rect.Width * RenderScaling) / RenderScaling,
        Math.Round(rect.Height * RenderScaling) / RenderScaling);
}
