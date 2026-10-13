using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>One tick of an axis: where it sits along the axis in pixels, and what it says.</summary>
/// <param name="Position">The position along the axis, in the layer's coordinates.</param>
/// <param name="Label">The text.</param>
public readonly record struct AxisTick(double Position, string Label);

/// <summary>
/// Turns what an axis worked out into the marks and labels to draw, and says how much room they need.
/// </summary>
/// <remarks>
/// <para>
/// A model rather than a visual, like everything else below the layers: it works out positions and text, and
/// a layer paints them. That split is what lets the gutter be measured during layout - the plot rectangle
/// depends on it - while the painting happens later, from the same numbers.
/// </para>
/// <para>
/// Labels are thinned to fit: an axis that would put text on top of text drops some of it instead. The first
/// label is always kept and the last one is kept when it still fits, because the two ends of an axis are the
/// two the eye looks for.
/// </para>
/// </remarks>
public sealed class AxisRenderer
{
    /// <summary>Creates a renderer for one dimension's axis.</summary>
    /// <param name="axis">The axis.</param>
    /// <param name="dimension">The dimension it belongs to; the vertical one is drawn sideways.</param>
    public AxisRenderer(IAxis axis, ChartDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(axis);
        ArgumentNullException.ThrowIfNull(dimension);

        Axis = axis;
        Dimension = dimension;
    }

    /// <summary>The axis being drawn.</summary>
    public IAxis Axis
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>The dimension this axis belongs to.</summary>
    public ChartDimension Dimension { get; }

    /// <summary>True when this axis runs up and down the chart.</summary>
    public bool IsVertical => Dimension == ChartDimension.Y;

    /// <summary>The size of the labels, in points.</summary>
    public double FontSize { get; set; } = 11;

    /// <summary>The face the labels are set in.</summary>
    public Typeface Typeface { get; set; } = new(FontFamily.Default);

    /// <summary>How long the tick marks are, in logical pixels.</summary>
    public double TickLength { get; set; } = 4;

    /// <summary>The gap between a tick mark and its label, in logical pixels.</summary>
    public double LabelGap { get; set; } = 4;

    /// <summary>
    /// The air left outside the labels, in logical pixels. Without it the widest label is flush against the
    /// edge of the control, which reads as a mistake - which is exactly how the first golden image read.
    /// </summary>
    public double LabelMargin { get; set; } = 4;

    /// <summary>Whether to draw the labels at all.</summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// The ticks to draw, in ascending position, with the labels thinned so that no two of them collide.
    /// </summary>
    /// <param name="layout">What the axis worked out.</param>
    /// <param name="plotBounds">The rectangle the data is mapped into.</param>
    /// <returns>The ticks; empty when the axis covers nothing.</returns>
    public IReadOnlyList<AxisTick> GetTicks(in AxisLayout layout, Rect plotBounds)
    {
        var ticks = new List<AxisTick>(layout.MajorTicks.Count);

        foreach (var unit in layout.MajorTicks)
        {
            ticks.Add(new AxisTick(Position(layout, unit, plotBounds), Axis.FormatValue(unit, layout.MajorStep)));
        }

        return ShowLabels ? Thin(ticks) : ticks;
    }

    /// <summary>
    /// How many pixels this axis needs beyond the plot: the tick marks, the gap and the widest label. Worked
    /// out from every label rather than the surviving ones, so that thinning cannot change how much room the
    /// chart gives the axis - which would move the plot and thin the labels again.
    /// </summary>
    /// <param name="layout">What the axis worked out.</param>
    /// <returns>The gutter, in logical pixels.</returns>
    public double MeasureGutter(in AxisLayout layout)
    {
        if (!ShowLabels)
        {
            return TickLength;
        }

        var widest = 0d;

        foreach (var unit in layout.MajorTicks)
        {
            var size = MeasureLabel(Axis.FormatValue(unit, layout.MajorStep));
            var extent = IsVertical ? size.Width : size.Height;

            if (extent > widest)
            {
                widest = extent;
            }
        }

        return TickLength + (widest > 0 ? LabelGap + widest + LabelMargin : 0);
    }

    /// <summary>Measures one label.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The size of the text as it would be drawn.</returns>
    public Size MeasureLabel(string text)
    {
        var formatted = CreateLabel(text, null);
        return new Size(formatted.Width, formatted.Height);
    }

    /// <summary>Lays a label out for drawing.</summary>
    /// <param name="text">The text.</param>
    /// <param name="brush">The brush to draw it with.</param>
    /// <returns>The laid-out text.</returns>
    public FormattedText CreateLabel(string text, IBrush? brush) =>
        new(
            text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface,
            FontSize,
            brush);

    private double Position(in AxisLayout layout, double unit, Rect plotBounds)
    {
        var fraction = layout.UnitRange.Normalize(unit);

        // Screen y grows downwards, so a bigger value is nearer the top; x is the other way round.
        return IsVertical
            ? plotBounds.Bottom - (fraction * plotBounds.Height)
            : plotBounds.X + (fraction * plotBounds.Width);
    }

    private double Extent(string label)
    {
        var size = MeasureLabel(label);
        return IsVertical ? size.Height : size.Width;
    }

    private IReadOnlyList<AxisTick> Thin(List<AxisTick> ticks)
    {
        if (ticks.Count < 2)
        {
            return ticks;
        }

        var kept = new List<AxisTick> { ticks[0] };

        for (var index = 1; index < ticks.Count; index++)
        {
            if (FitsAfter(kept[^1], ticks[index]))
            {
                kept.Add(ticks[index]);
            }
        }

        var last = ticks[^1];

        // The two ends of an axis are the two the eye looks for, so the far one is worth squeezing in when the
        // greedy pass from the near end did not reach it.
        if (!kept[^1].Equals(last) && FitsAfter(kept[^1], last))
        {
            kept.Add(last);
        }

        return kept;
    }

    /// <summary>
    /// Whether a label fits after the one before it, along the axis rather than up the screen: on a vertical
    /// axis the labels march downwards as the values go up.
    /// </summary>
    private bool FitsAfter(AxisTick previous, AxisTick current)
    {
        var (previousLow, previousHigh) = Interval(previous);
        var (nextLow, nextHigh) = Interval(current);

        var distance = IsVertical ? previousLow - nextHigh : nextLow - previousHigh;

        return distance > LabelGap;
    }

    private (double Low, double High) Interval(AxisTick tick)
    {
        var half = Extent(tick.Label) / 2;

        return (tick.Position - half, tick.Position + half);
    }
}
