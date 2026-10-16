using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>One tick of an axis: where it sits along the axis in pixels, and what it says.</summary>
/// <param name="Position">The position along the axis, in the layer's coordinates.</param>
/// <param name="Label">The text.</param>
public readonly record struct AxisTick(double Position, string Label);

/// <summary>Which edge of the plot an axis is drawn against.</summary>
/// <remarks>
/// Flex's <c>placement</c> style, as the two cases that matter rather than as four names that can be combined
/// wrongly: an axis runs along a dimension, so the only question is whether it is on the near edge of the plot
/// or the far one - below or above a horizontal axis, left or right of a vertical one. A second Y axis, which is
/// the whole reason to have more than one scale on a chart, is the far one.
/// </remarks>
public enum AxisPlacement
{
    /// <summary>The near edge: below a horizontal axis, to the left of a vertical one.</summary>
    Start,

    /// <summary>The far edge: above a horizontal axis, to the right of a vertical one.</summary>
    End,
}

/// <summary>Which side of the axis line a tick mark is drawn on.</summary>
/// <remarks>
/// <para>
/// Flex's <c>tickPlacement</c>, with its four values kept: the marks are the axis' own and where they sit is a
/// question about the paper rather than about the readings - a chart with a measured grid wants ticks inside the
/// plot, where they line up with the gridlines and take no room from the margin, and a chart read against its
/// edge wants them outside it.
/// </para>
/// <para>
/// Named for the axis rather than after Flex's own word for it, because Avalonia already has a
/// <c>TickPlacement</c> - a slider's, with a different set of values - and a host that has both namespaces open
/// would have to qualify one of them to say which it meant.
/// </para>
/// </remarks>
public enum AxisTickPlacement
{
    /// <summary>Nowhere: the marks are not drawn, and the axis is its line and its labels.</summary>
    None,

    /// <summary>Outward, away from the plot; what an axis does unless it is told otherwise.</summary>
    Outside,

    /// <summary>Inward, into the plot, taking no room from the margin.</summary>
    Inside,

    /// <summary>Both ways, across the axis line: half in and half out.</summary>
    Cross,
}

/// <summary>Which way a title runs when the axis it belongs to runs up the chart.</summary>
/// <remarks>
/// A title on an axis that runs across the chart is written left to right like any other line of text; one on an
/// axis that runs up the chart has to be turned, and there are two ways to turn it. Flex has the same two under
/// <c>verticalAxisTitleAlignment</c>, and calls the downward one its default; we take the upward one, because it is
/// what a reader of most charts finds there.
/// </remarks>
public enum AxisTitleDirection
{
    /// <summary>Read upwards, the bottom of the letters towards the bottom of the chart.</summary>
    BottomToTop,

    /// <summary>Read downwards, the letters turned the other way.</summary>
    TopToBottom,
}

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

    /// <summary>
    /// Raised when a setting changed, so that whoever draws this axis can draw it again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These settings are the caller's to change at any time - a knob beside a chart, a preference, a theme - and
    /// they change <em>how</em> the axis is drawn and how much room it takes rather than what it covers, so
    /// nothing else in the library notices them on its own. This is where a chart is told: hiding the labels, for
    /// one, gives the plot back the room the gutter was taking.
    /// </para>
    /// <para>
    /// The axis' own settings are raised through here as well, because they take room the same way: a title, a
    /// minimum, a step that puts wider labels under the axis all move the plot, and a chart that only painted
    /// itself again would paint the same room with something else in it. Without this an axis was worked out
    /// again, drawn again, and never laid out again - which is a title that appears in the gutter measured for
    /// no title.
    /// </para>
    /// </remarks>
    public event EventHandler? Changed;

    /// <summary>The axis being drawn.</summary>
    public IAxis Axis
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (field is AxisBase before)
            {
                before.Changed -= OnAxisChanged;
            }

            if (value is AxisBase after)
            {
                after.Changed += OnAxisChanged;
            }

            Set(ref field, value);
        }
    }

    /// <summary>The dimension this axis belongs to.</summary>
    public ChartDimension Dimension { get; }

    /// <summary>True when this axis runs up and down the chart.</summary>
    public bool IsVertical => Dimension == ChartDimension.Y;

    /// <summary>The size of the labels, in points.</summary>
    public double FontSize
    {
        get;
        set => Set(ref field, value);
    } = 11;

    /// <summary>The face the labels are set in.</summary>
    public Typeface Typeface
    {
        get;
        set => Set(ref field, value);
    } = new(FontFamily.Default);

    /// <summary>How long the tick marks are, in logical pixels.</summary>
    public double TickLength
    {
        get;
        set => Set(ref field, value);
    } = 4;

    /// <summary>The gap between a tick mark and its label, in logical pixels.</summary>
    public double LabelGap
    {
        get;
        set => Set(ref field, value);
    } = 4;

    /// <summary>
    /// The air left outside the labels, in logical pixels. Without it the widest label is flush against the
    /// edge of the control, which reads as a mistake - which is exactly how the first golden image read.
    /// </summary>
    public double LabelMargin
    {
        get;
        set => Set(ref field, value);
    } = 4;

    /// <summary>
    /// The air between the outermost label and the axis' title, in logical pixels; the same four as the gap
    /// between a mark and its label, so that the two pieces of writing stand the same distance apart as the
    /// marks they belong to.
    /// </summary>
    public double TitleGap
    {
        get;
        set => Set(ref field, value);
    } = 4;

    /// <summary>Which way a title on a vertical axis reads; upwards by default.</summary>
    public AxisTitleDirection TitleDirection
    {
        get;
        set => Set(ref field, value);
    } = AxisTitleDirection.BottomToTop;

    /// <summary>
    /// How far the labels are turned from upright, in degrees; zero by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Flex's <c>labelRotation</c>, in its unit: a positive angle turns a label anticlockwise on screen, so the
    /// label at the foot of a chart rises to the right - the way a date or a name is usually turned when there
    /// are more of them than fit upright.
    /// </para>
    /// <para>
    /// A label is measured by the box it needs once it is turned, and that box is what is drawn, so the room the
    /// axis asks for is the room it uses. Turning a label makes it thicker across the axis and narrower along it:
    /// the gutter grows, and more labels fit in the row.
    /// </para>
    /// <para>
    /// A quarter turn either way is the limit, because a label turned further than that reads upside down, and a
    /// label nobody can read is not worth the room it takes.
    /// </para>
    /// </remarks>
    public double LabelRotation
    {
        get;
        set
        {
            if (double.IsNaN(value) || value < -90 || value > 90)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "a label may be turned up to a quarter turn either way, and no further");
            }

            Set(ref field, value);
        }
    }

    /// <summary>
    /// Whether the labels take two rows instead of one; one row by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Flex's <c>canStagger</c>. The rows alternate along the axis, so a label only has to clear the one two
    /// places away rather than its nearest neighbour - which is the whole of what staggering buys: about twice
    /// as many labels in the same room, at the price of a second row of it.
    /// </para>
    /// <para>
    /// Off by default, because a chart whose labels fit in one row should have one: this is a setting the host
    /// makes, and not a compromise the axis reaches for on its own.
    /// </para>
    /// </remarks>
    public bool StaggerLabels
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>Whether to draw the labels at all.</summary>
    public bool ShowLabels
    {
        get;
        set => Set(ref field, value);
    } = true;

    /// <summary>Which edge of the plot the marks and labels are drawn against; the near edge by default.</summary>
    public AxisPlacement Placement
    {
        get;
        set => Set(ref field, value);
    } = AxisPlacement.Start;

    /// <summary>Which side of the axis line the tick marks are drawn on; outside the plot by default.</summary>
    /// <remarks>
    /// A setting about the paper rather than about the readings, and it changes how much room the axis needs:
    /// a tick drawn inward takes none of the margin, and half of one drawn across takes half of it - which is what
    /// <see cref="OutwardReach"/> leaves.
    /// </remarks>
    public AxisTickPlacement TickPlacement
    {
        get;
        set => Set(ref field, value);
    } = AxisTickPlacement.Outside;

    /// <summary>How far a tick mark reaches away from the plot, in logical pixels.</summary>
    /// <remarks>
    /// None for a mark drawn inward or not at all, and half the length for one drawn across - which is what the
    /// gutter is measured with, and what the layer puts a label's gap beyond.
    /// </remarks>
    internal double OutwardReach => TickPlacement switch
    {
        AxisTickPlacement.Outside => TickLength,
        AxisTickPlacement.Cross => TickLength / 2,
        _ => 0,
    };

    /// <summary>Sets a setting, and says that it changed when it did.</summary>
    /// <typeparam name="T">What the setting is.</typeparam>
    /// <param name="setting">The setting.</param>
    /// <param name="value">What it is being set to.</param>
    private void Set<T>(ref T setting, T value)
    {
        if (EqualityComparer<T>.Default.Equals(setting, value))
        {
            return;
        }

        setting = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What an axis says changed is what this renderer says changed.</summary>
    /// <param name="sender">The axis.</param>
    /// <param name="e">Unused.</param>
    private void OnAxisChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// The ticks to draw, in ascending position, with the labels thinned so that no two of them collide - or, when
    /// the labels are staggered, so that no two in the same row do.
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
    /// How many pixels this axis needs beyond the plot: the tick marks, the gap and the labels - one row of the
    /// thickest of them, or two when they are staggered - and then the title, if the axis has one.
    /// </summary>
    /// <param name="layout">What the axis worked out.</param>
    /// <returns>The gutter, in logical pixels.</returns>
    /// <remarks>
    /// Worked out from every label rather than the surviving ones, so that thinning cannot change how much room the
    /// chart gives the axis - which would move the plot and thin the labels again.
    /// </remarks>
    public double MeasureGutter(in AxisLayout layout)
    {
        var title = MeasureTitle();

        return MeasureLabelRoom(layout) + (title > 0 ? TitleGap + title : 0);
    }

    /// <summary>
    /// How many pixels the marks and the labels need beyond the plot, without the title.
    /// </summary>
    /// <param name="layout">What the axis worked out.</param>
    /// <returns>The room, in logical pixels.</returns>
    /// <remarks>
    /// Apart from the gutter so that the title can be placed beyond it: the two pieces of writing are stacked, so
    /// whoever draws them needs to know where the first one ends.
    /// </remarks>
    internal double MeasureLabelRoom(in AxisLayout layout)
    {
        if (!ShowLabels)
        {
            return OutwardReach;
        }

        var across = MeasureLabelRow(layout);

        // Two rows cost two rows of room, because the ones in the second sit a whole row further out.
        return OutwardReach + (across > 0 ? LabelGap + ((StaggerLabels ? 2 : 1) * across) + LabelMargin : 0);
    }

    /// <summary>
    /// How thick the axis' title is measured from the plot, or zero when there is no title to draw.
    /// </summary>
    /// <returns>The room, in logical pixels.</returns>
    /// <remarks>
    /// The title's height either way round: on a horizontal axis it is written as it is, and on a vertical one it is
    /// turned a quarter turn, and a quarter turn swaps the two extents of the box - so one line of text is one
    /// line thick from the plot whichever axis it belongs to.
    /// </remarks>
    internal double MeasureTitle() =>
        string.IsNullOrEmpty(Axis.Title) ? 0 : MeasureLabel(Axis.Title!).Height;

    /// <summary>
    /// How far this axis' outermost labels reach past the ends of the plot, along the axis.
    /// </summary>
    /// <param name="layout">The axis' layout.</param>
    /// <returns>The overhang, in pixels; zero when the labels are off or none are drawn.</returns>
    /// <remarks>
    /// A label is centred on its tick, and the outermost tick of an axis sits on the edge of the plot - so half of
    /// that label falls beyond the plot. Without room made for it, it is cut away by the edge of the control,
    /// which is what the topmost label of a vertical axis and the last one of a horizontal axis used to do.
    /// </remarks>
    public double MeasureOverhang(in AxisLayout layout)
    {
        if (!ShowLabels)
        {
            return 0;
        }

        var longest = 0d;

        foreach (var unit in layout.MajorTicks)
        {
            // Along the axis, not across it: the labels of a vertical axis run up the plot, so what reaches past
            // its top and bottom is half of what one of them takes up there.
            var along = MeasureLabelBox(Axis.FormatValue(unit, layout.MajorStep)).Along;

            if (along > longest)
            {
                longest = along;
            }
        }

        return longest / 2;
    }

    /// <summary>
    /// How thick one row of labels is across the axis, taken from the thickest label there is.
    /// </summary>
    /// <param name="layout">What the axis worked out.</param>
    /// <returns>The thickness, in logical pixels; zero when there are no labels.</returns>
    /// <remarks>
    /// The same number the gutter is measured with and the layer places each label's row with, so that the room
    /// the axis asks for and the room it takes up are one piece of arithmetic rather than two.
    /// </remarks>
    internal double MeasureLabelRow(in AxisLayout layout)
    {
        var thickest = 0d;

        foreach (var unit in layout.MajorTicks)
        {
            var across = MeasureLabelBox(Axis.FormatValue(unit, layout.MajorStep)).Across;

            if (across > thickest)
            {
                thickest = across;
            }
        }

        return thickest;
    }

    /// <summary>Measures one label.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The size of the text as it would be drawn.</returns>
    public Size MeasureLabel(string text)
    {
        var formatted = CreateLabel(text, null);
        return new Size(formatted.Width, formatted.Height);
    }

    /// <summary>
    /// Measures one label as the box it needs once it is turned: how thick it is across the axis, and how much
    /// room it takes up along it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The two extents, in logical pixels.</returns>
    internal (double Across, double Along) MeasureLabelBox(string text) => MeasureLabelBox(MeasureLabel(text));

    /// <summary>
    /// Measures a label of a known size as the box it needs once it is turned.
    /// </summary>
    /// <param name="size">The size of the label upright.</param>
    /// <returns>The two extents, in logical pixels.</returns>
    internal (double Across, double Along) MeasureLabelBox(Size size)
    {
        var radians = LabelRotation * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));

        // The bounding box of a rectangle turned by an angle: the upright width and height each cast a shadow on
        // both axes, and the box is what the two shadows add up to.
        var width = (size.Width * cosine) + (size.Height * sine);
        var height = (size.Width * sine) + (size.Height * cosine);

        // Across the axis is the thickness of a row of labels and along it is the room one takes up there, and the
        // two are the other way round on an axis that runs up the chart: its labels march up the plot.
        return IsVertical ? (width, height) : (height, width);
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

    /// <summary>Lays the axis' title out for drawing.</summary>
    /// <param name="brush">The brush to draw it with.</param>
    /// <returns>The laid-out title; empty text when the axis has no title.</returns>
    /// <remarks>
    /// Set in the same face and the same size as the labels, and wearing a brush of its own: the design brief gives
    /// the title one colour and the labels another, and nothing else about it is meant to stand out from them.
    /// </remarks>
    public FormattedText CreateTitle(IBrush? brush) => CreateLabel(Axis.Title ?? string.Empty, brush);

    private double Position(in AxisLayout layout, double unit, Rect plotBounds)
    {
        var fraction = layout.UnitRange.Normalize(unit);

        // Screen y grows downwards, so a bigger value is nearer the top; x is the other way round.
        return IsVertical
            ? plotBounds.Bottom - (fraction * plotBounds.Height)
            : plotBounds.X + (fraction * plotBounds.Width);
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
            if (Clear(kept, ticks[index]))
            {
                kept.Add(ticks[index]);
            }
        }

        var last = ticks[^1];

        // The two ends of an axis are the two the eye looks for, so the far one is worth squeezing in when the
        // greedy pass from the near end did not reach it.
        if (!kept[^1].Equals(last) && Clear(kept, last))
        {
            kept.Add(last);
        }

        return kept;
    }

    /// <summary>
    /// Whether a label has room among the ones already kept.
    /// </summary>
    /// <param name="kept">The labels kept so far, in order; the row a label lands in is its place in this list.</param>
    /// <param name="current">The label being considered.</param>
    /// <returns>True when nothing stands where the label would go.</returns>
    /// <remarks>
    /// The one to clear is the one before it in its own row, which is the one before that in the list when the
    /// labels are staggered: neighbours in the other row overlap along the axis, and that overlap is the whole
    /// point of staggering them.
    /// </remarks>
    private bool Clear(List<AxisTick> kept, AxisTick current)
    {
        var back = StaggerLabels ? 2 : 1;

        return kept.Count < back || FitsAfter(kept[^back], current);
    }

    /// <summary>
    /// Whether a label fits after the one before it in the same row, along the axis rather than up the screen: on
    /// a vertical axis the labels march downwards as the values go up.
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
        var half = MeasureLabelBox(tick.Label).Along / 2;

        return (tick.Position - half, tick.Position + half);
    }
}
