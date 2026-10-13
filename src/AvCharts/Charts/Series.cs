using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>What a series draws where a record has no value on one of its dimensions.</summary>
/// <remarks>
/// A missing value is not a value of zero, and the default says so: a break in the line is the truthful picture
/// of a reading that never arrived, and a chart that smooths it over is telling a story the data does not.
/// The other two modes are there because a caller sometimes knows better - a counter that missed a tick was
/// idle, not broken - and because saying so is the caller's decision rather than the library's.
/// </remarks>
public enum NullValueMode
{
    /// <summary>A break: the run ends where the value is missing and starts again after it. The default.</summary>
    Gap,

    /// <summary>The record is left out and its neighbours are joined, as if the reading had never been asked for.</summary>
    Connect,

    /// <summary>The record is drawn where the axis puts zero - for an axis that has a zero to put it at.</summary>
    Zero,
}

/// <summary>
/// A set of data drawn one way: the element a transform knows about, and the unit a chart's series list is
/// made of.
/// </summary>
/// <remarks>
/// <para>
/// The four hooks below are the extension contract, and they run in this order: <see cref="UpdateData"/> reads
/// the source, <see cref="DescribeData"/> says what the axes have to cover, <see cref="BuildGeometry"/> turns
/// values into ink - once per change, not once per frame - and <see cref="Draw"/> puts that ink on the screen.
/// A subclass that keeps to the order can do anything; a subclass that does not will see an axis sized for
/// data it no longer has.
/// </para>
/// <para>
/// Nothing here holds a <c>Visual</c>. A series is a model, and the layer it is registered with is what paints
/// it, which is what keeps ten thousand points from costing ten thousand visuals.
/// </para>
/// </remarks>
public abstract class Series : IChartElement
{
    private bool _dataIsStale = true;
    private bool _geometryIsStale = true;
    private NullValueMode _nullValueMode = NullValueMode.Gap;

    /// <summary>Raised when the series changed and whatever draws it should repaint.</summary>
    public event EventHandler? Invalidated;

    /// <summary>
    /// Raised when a record was handed to the series from somewhere other than the thread it is drawn on.
    /// </summary>
    /// <remarks>
    /// Raised on the thread that pushed, which is the point of it: whoever draws the series hears about the push
    /// there and asks for a pass on the drawing thread. Nothing may be painted from the handler.
    /// </remarks>
    public event EventHandler? Pushed;

    /// <summary>The name shown in a legend or a data tip.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// What to draw where a record has no value on one of its dimensions; a break by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drawing decision rather than a data one: the values the series kept are the same whichever mode is in
    /// force, and so is what the axes cover, so switching modes repaints without reading anything again. That is
    /// also why <see cref="NullValueMode.Zero"/> does not ask the axis to make room for zero: a missing value is
    /// not a data value, and an axis that grew to include it would be describing data that is not there. A
    /// caller who wants zero in view sets <see cref="AxisBase.Minimum"/>.
    /// </para>
    /// <para>
    /// In a stack, a missing value is drawn with no height but is still missing rather than zero, and a data tip
    /// says which of the two it is - that part belongs to the stacking step and is recorded in the design brief.
    /// </para>
    /// </remarks>
    public NullValueMode NullValueMode
    {
        get => _nullValueMode;
        set
        {
            if (_nullValueMode == value)
            {
                return;
            }

            _nullValueMode = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The transform this series is registered with, set when it is added to one.</summary>
    public DataTransform? Transform { get; internal set; }

    /// <summary>
    /// Whether this series brings its own colours, and so takes no slot in the chart's palette.
    /// </summary>
    /// <remarks>
    /// A series that is coloured by hand keeps the rest of the chart where it was: the next series still gets
    /// the slot this one declined. That is where this differs from the Flex rule, under which setting a colour
    /// on the first series shifted every series after it.
    /// </remarks>
    public virtual bool HasOwnColours => false;

    /// <summary>
    /// The stroke the chart gave this series out of its palette, when the series has none of its own. Set by
    /// the chart; a series paints with its own colour first and with this one otherwise.
    /// </summary>
    public Pen? PaletteStroke
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>
    /// The fill the chart gave this series out of its palette, when the series has none of its own. Set by the
    /// chart, like <see cref="PaletteStroke"/>; a series that fills the area under itself uses it.
    /// </summary>
    public IBrush? PaletteFill
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Paints the series: brings its data up to date, rebuilds its geometry if the mapping moved, then draws.
    /// Called by the layer the series is registered with, inside that layer's render pass.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="renderContext">Where the plot is and how to map values into it.</param>
    public void Paint(DrawingContext context, in SeriesRenderContext renderContext)
    {
        ArgumentNullException.ThrowIfNull(context);

        EnsureData();

        // The describe pass has to come before the geometry is built, so the axes have already worked out what
        // they cover by the time a value is mapped. Asking here is what keeps the four phases in order.
        renderContext.Transform.Update();

        if (_geometryIsStale)
        {
            BuildGeometry(renderContext);

            // Cleared after building, not before: mapping a value can run the transform's own update, which
            // tells every element - this one included - that the mapping moved. What was just built is what
            // that update produced, so it is fresh.
            _geometryIsStale = false;
        }

        Draw(context, renderContext);
    }

    /// <summary>Phase 1: reads the source into whatever the rest of the series works with.</summary>
    protected abstract void UpdateData();

    /// <summary>Phase 2: declares what this series needs each axis to cover, in unit space.</summary>
    /// <param name="description">The description to add to. Missing values are left out.</param>
    protected abstract void DescribeData(DataDescription description);

    /// <summary>Phase 3: turns the current values into geometry, given where the plot is.</summary>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    protected abstract void BuildGeometry(in SeriesRenderContext context);

    /// <summary>Phase 4: paints the geometry built in phase 3.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="renderContext">Where the plot is and how to map values into it.</param>
    protected abstract void Draw(DrawingContext context, in SeriesRenderContext renderContext);

    /// <summary>Marks the data stale, which also makes the geometry stale, and asks for a repaint.</summary>
    /// <remarks>
    /// What a host calls when its source changed without saying so - a plain list it just edited - and what a
    /// subclass calls when it read something the series could not see.
    /// </remarks>
    public void InvalidateData()
    {
        _dataIsStale = true;
        _geometryIsStale = true;
        Invalidate();
    }

    /// <summary>Marks the geometry stale without touching the data, and asks for a repaint.</summary>
    protected void InvalidateGeometry()
    {
        _geometryIsStale = true;
        Invalidate();
    }

    /// <summary>
    /// Applies everything that was pushed to the series, in one batch, on the thread the series is drawn on.
    /// </summary>
    /// <returns>How many records were applied; nothing at all for a series that cannot be pushed to.</returns>
    /// <remarks>
    /// The chart calls this once a frame, which is what keeps a feed of a thousand points a second from costing a
    /// thousand repaints: the frame costs one.
    /// </remarks>
    public virtual int ApplyPushed() => 0;

    /// <summary>Raises <see cref="Pushed"/>, for a subclass that takes records from another thread.</summary>
    protected void OnPushed() => Pushed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Asks for a repaint, and tells the transform that the ranges it worked out may no longer be right.
    /// </summary>
    protected void Invalidate()
    {
        Transform?.InvalidateRange();
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reads the source again, if it changed since the last time.</summary>
    protected void EnsureData()
    {
        if (!DataIsStale)
        {
            return;
        }

        _dataIsStale = false;
        UpdateData();
    }

    /// <summary>
    /// Whether the data has to be read again: either something said so, or a subclass has a reason of its own.
    /// </summary>
    protected bool DataIsStale => _dataIsStale || ValuesAreStale;

    /// <summary>
    /// Whether what the series kept is stale for a reason the last read could not have known about.
    /// </summary>
    /// <remarks>
    /// A series whose values are measured in something the chart decides - unit space, which the axes set - says
    /// so here, and the next question about the series reads the source again. Left alone, nothing but a change
    /// to the data itself makes the values stale.
    /// </remarks>
    protected virtual bool ValuesAreStale => false;

    void IChartElement.DescribeData(DataDescription description)
    {
        EnsureData();
        DescribeData(description);
    }

    void IChartElement.MappingChanged() => _geometryIsStale = true;
}
