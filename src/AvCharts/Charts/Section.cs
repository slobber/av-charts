using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// A stretch of the plot marked out and, if it is named, named: the shaded band between two readings on one axis.
/// </summary>
/// <remarks>
/// <para>
/// The two readings are read on <em>one</em> dimension - the band runs the whole way across the plot on the other -
/// which is what makes it a section rather than a shape: "the third week", "between 90 and 100", "everything above
/// the budget". Flex has no such element, so the shape is the obvious one rather than a ported one; what is
/// borrowed is the vocabulary, and the same section written along an axis instead of across the plot is
/// <see cref="AxisSection"/>.
/// </para>
/// <para>
/// Either end may be left out, and then the band runs to the edge of the plot: that is how "everything above this"
/// is drawn without the host having to know where the plot ends. A value the axis cannot place - one of the wrong
/// kind, or a missing one - draws nothing at all rather than half a band, because a section nobody can place is a
/// mistake rather than a reading.
/// </para>
/// <para>
/// A section does not stretch the axes to make room for itself. An annotation outside the range the chart covers is
/// not drawn, which keeps a band from quietly changing the scale of the readings it is drawn over; a host that
/// wants the band on screen gives the axis the range that contains it.
/// </para>
/// </remarks>
public class Section : ChartElement
{
    private ChartDimension _dimension = ChartDimension.X;
    private ChartValue? _from;
    private ChartValue? _to;
    private string? _name;

    /// <summary>Which dimension the two readings are read on; the horizontal one unless it is changed.</summary>
    /// <remarks>
    /// A section on the horizontal dimension is a slab standing up the plot, one on the vertical dimension a band
    /// lying across it, which is the whole of what the setting decides.
    /// </remarks>
    public ChartDimension Dimension
    {
        get => _dimension;
        set
        {
            if (_dimension == value)
            {
                return;
            }

            _dimension = value;
            Invalidate();
        }
    }

    /// <summary>The reading the section starts at; unset, it starts at the edge of the plot.</summary>
    public ChartValue? From
    {
        get => _from;
        set
        {
            if (Nullable.Equals(_from, value))
            {
                return;
            }

            _from = value;
            Invalidate();
        }
    }

    /// <summary>The reading it ends at; unset, it ends at the edge of the plot.</summary>
    public ChartValue? To
    {
        get => _to;
        set
        {
            if (Nullable.Equals(_to, value))
            {
                return;
            }

            _to = value;
            Invalidate();
        }
    }

    /// <summary>What the band is filled with; unset, it is not filled and only its ends are drawn.</summary>
    public IBrush? Fill { get; set; }

    /// <summary>The pen the two ends of the band are drawn with; unset, the ends are not drawn.</summary>
    /// <remarks>
    /// The ends rather than the whole outline: the other two sides of the band are the edges of the plot, and a line
    /// drawn along those says nothing about the section while making the plot look framed.
    /// </remarks>
    public Pen? Border { get; set; }

    /// <summary>What the band is called; unset, nothing is written in it.</summary>
    public string? Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            Invalidate();
        }
    }

    /// <summary>The brush the name is written in; unset, the border's brush, and then the fill's own colour.</summary>
    public IBrush? NameBrush { get; set; }

    /// <summary>The size the name is written at, in points.</summary>
    public double FontSize { get; set; } = 11;

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var plot = renderContext.PlotBounds;

        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        // One dimension, and then the whole of the other one: an unset end reaches the edge it is nearest.
        if (!Near(in renderContext, plot, _from, start: true, out var low) ||
            !Near(in renderContext, plot, _to, start: false, out var high))
        {
            return;
        }

        var band = _dimension == ChartDimension.X
            ? new Rect(Math.Min(low, high), plot.Y, Math.Abs(high - low), plot.Height)
            : new Rect(plot.X, Math.Min(low, high), plot.Width, Math.Abs(high - low));

        if (Fill is { } fill)
        {
            context.FillRectangle(fill, band);
        }

        if (Border is { } border)
        {
            foreach (var end in new[] { low, high })
            {
                context.DrawLine(
                    border,
                    _dimension == ChartDimension.X ? new Point(end, plot.Y) : new Point(plot.X, end),
                    _dimension == ChartDimension.X ? new Point(end, plot.Bottom) : new Point(plot.Right, end));
            }
        }

        DrawName(context, renderContext, band);
    }

    /// <summary>
    /// Places one end of the section, treating an unset end as the edge of the plot rather than as a missing
    /// value - which is the one place a section is more forgiving than a shape drawn from readings.
    /// </summary>
    /// <param name="renderContext">Where the plot is and how to map values into it.</param>
    /// <param name="plot">The plot.</param>
    /// <param name="value">The reading, if there is one.</param>
    /// <param name="start">Whether this is the end the section starts at.</param>
    /// <param name="position">Where it sits, in pixels.</param>
    /// <returns><see langword="false"/> when the value is there and cannot be placed.</returns>
    private bool Near(
        in SeriesRenderContext renderContext,
        Rect plot,
        ChartValue? value,
        bool start,
        out double position)
    {
        if (value is not { } reading)
        {
            position = _dimension == ChartDimension.X
                ? (start ? plot.Left : plot.Right)
                : (start ? plot.Bottom : plot.Top);

            return true;
        }

        return renderContext.Transform.TryValueToPosition(_dimension, reading, out position);
    }

    /// <summary>Writes the name inside the band, at the end it starts at.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="renderContext">Where the plot is and how to map values into it.</param>
    /// <param name="band">The band.</param>
    private void DrawName(DrawingContext context, in SeriesRenderContext renderContext, Rect band)
    {
        if (string.IsNullOrEmpty(_name))
        {
            return;
        }

        var text = new FormattedText(
            _name,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            FontSize,
            NameBrush ?? Border?.Brush ?? FillInk());

        // Upright in either direction, standing just inside the plot at the start of the band: a name that turned
        // itself sideways would be one more thing to read at an angle on a chart that has none.
        context.DrawText(
            text,
            _dimension == ChartDimension.X
                ? new Point(band.X + 3, renderContext.PlotBounds.Y + 3)
                : new Point(renderContext.PlotBounds.X + 3, band.Y + 3));
    }

    /// <summary>
    /// The ink a name is written in when the host gave one: the fill's own colour at full strength, which is the
    /// same order of choice a box's middle line and an error bar's hairline make - a host that set one ink gets the
    /// other thing drawn in it rather than in a colour nobody chose.
    /// </summary>
    /// <returns>The brush, or null when there is nothing to take a colour from.</returns>
    private IBrush? FillInk() =>
        (Fill as ISolidColorBrush)?.Color is { } colour ? new SolidColorBrush(colour) : null;
}
