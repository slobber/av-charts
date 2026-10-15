using Avalonia;
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
    private bool _visible = true;
    private NullValueMode _nullValueMode = NullValueMode.Gap;
    private IAxis? _horizontalAxis;
    private IAxis? _verticalAxis;

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

    /// <summary>Raised when the series is shown or hidden.</summary>
    /// <remarks>
    /// Its own event rather than <see cref="Invalidated"/>: hiding a series is not only a repaint, it takes the
    /// series out of what the axes cover and out of the room the plot gives up for them, and only whoever lays
    /// the chart out can do that.
    /// </remarks>
    public event EventHandler? VisibleChanged;

    /// <summary>Whether the series is drawn, ranged and laid out; on by default.</summary>
    /// <remarks>
    /// <para>
    /// A hidden series takes part in nothing: it is not drawn, the axes do not grow to cover it, it is not
    /// stacked and does not stand in a band, an axis it brought of its own is not drawn, and no pointer or data
    /// tip can find it. The one thing it keeps is its place in the chart's list of series, which is what makes
    /// showing it again a matter of one property - and what a legend switches.
    /// </para>
    /// <para>
    /// Flex has no equivalent. There, hiding a series is the application's business, which means the application
    /// also has to keep it out of the axes, the stacks and the legend by hand.
    /// </para>
    /// </remarks>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
            {
                return;
            }

            _visible = value;

            // The axes have one fewer series to cover, or one more. Whoever lays the chart out does the work -
            // they are told by the event below - and this keeps a transform that is not behind a chart honest.
            Transform?.InvalidateRange();
            VisibleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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
    /// The axis this series is measured against horizontally, or null for the chart's own.
    /// </summary>
    /// <remarks>
    /// A series with an axis of its own is measured, ranged and labelled by that one, which is how two series on
    /// one chart can have two scales - the second Y axis a reading in millimetres beside one in inches wants.
    /// </remarks>
    public IAxis? HorizontalAxis
    {
        get => _horizontalAxis;
        set => SetAxis(ChartDimension.X, value);
    }

    /// <summary>The axis this series is measured against vertically, or null for the chart's own.</summary>
    public IAxis? VerticalAxis
    {
        get => _verticalAxis;
        set => SetAxis(ChartDimension.Y, value);
    }

    /// <summary>Gives this series an axis of its own for a dimension, or takes it away with null.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="axis">The axis, or null to be measured against the chart's.</param>
    public void SetAxis(ChartDimension dimension, IAxis? axis)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        if (ReferenceEquals(AxisOn(dimension), axis))
        {
            return;
        }

        if (dimension == ChartDimension.X)
        {
            _horizontalAxis = axis;
        }
        else if (dimension == ChartDimension.Y)
        {
            _verticalAxis = axis;
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "A series is measured against the two dimensions of a Cartesian chart.");
        }

        // The values are kept in the axis' own arithmetic, so another axis leaves them meaning something else;
        // and the chart has one more axis to resolve.
        Transform?.InvalidateRange();
        InvalidateData();
    }

    /// <summary>The axis of this series for a dimension: its own where it named one, the chart's otherwise.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The axis, or null when neither the series nor its chart has one.</returns>
    public IAxis? AxisFor(ChartDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        return AxisOn(dimension) ?? Transform?.GetAxis(dimension);
    }

    /// <summary>The axis this series was given for a dimension, ignoring the chart's own.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The axis, or null.</returns>
    internal IAxis? AxisOn(ChartDimension dimension) =>
        dimension == ChartDimension.X ? _horizontalAxis
            : dimension == ChartDimension.Y ? _verticalAxis
            : null;

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

            // The pen a series is drawn with, and nothing about what the axes cover: a range that moved would make
            // every layer lay itself out again and every series build its geometry again, which is a chart's worth
            // of work to change a colour.
            Repaint();
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
            Repaint();
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

    /// <summary>The colour the series is drawn in, for a data tip or a legend to show beside it.</summary>
    /// <remarks>
    /// The series' own colour when it has one, and the colour the chart took out of its palette for it
    /// otherwise - which is the pair a series paints with, so a tip and the line always agree.
    /// </remarks>
    public virtual IBrush? Color => PaletteStroke?.Brush;

    /// <summary>
    /// Takes the two colours a reading is drawn in when it went up and when it went down, as the chart found
    /// them in the theme.
    /// </summary>
    /// <param name="rise">The colour of a reading that went up.</param>
    /// <param name="fall">The colour of one that went down.</param>
    /// <remarks>
    /// A series that draws the two differently - a candle, a bar of a price - wears these; every other series
    /// has nothing to do with them, which is why this does nothing unless it is overridden. They come from tokens
    /// rather than from slots of the palette because which of the two a reading wears is a fact about the reading
    /// rather than about where the series stands among the others.
    /// </remarks>
    public virtual void TakePriceColours(IBrush rise, IBrush fall)
    {
        _ = rise;
        _ = fall;
    }

    /// <summary>
    /// Takes the chart's whole palette, for a series that colours each of its records rather than itself.
    /// </summary>
    /// <param name="strokes">The palette's pens, in slot order.</param>
    /// <param name="fills">The palette's brushes, in the same order.</param>
    /// <param name="first">The slot this series would have taken, which is where its records start.</param>
    /// <remarks>
    /// <para>
    /// A line is one colour, so the slot it was given is the whole of what it needs and this does nothing for
    /// it. A pie is a colour per wedge, and the palette is the only place those come from - so it is handed over
    /// rather than looked up, the same way the two financial colours are.
    /// </para>
    /// <para>
    /// The slots are what the theme says and the <paramref name="first"/> is where the series stands among the
    /// others, so a pie beside a line does not start on the line's colour. Which slot a record takes is the
    /// series' own business - the pie takes them in the order its records are recorded, and cycles when it has
    /// more records than the palette holds.
    /// </para>
    /// </remarks>
    public virtual void TakePalette(IReadOnlyList<Pen?> strokes, IReadOnlyList<IBrush?> fills, int first)
    {
        _ = strokes;
        _ = fills;
        _ = first;
    }

    /// <summary>Takes the ink the chart writes in, for a series that writes anything of its own.</summary>
    /// <param name="ink">The brush the chart's own text is written in.</param>
    /// <remarks>
    /// A pie's labels are the one text a series draws, and the ink for them is the chart's - the same token the
    /// axis labels are written in, because a reader does not separate a chart's words from each other. It does
    /// nothing for a series that draws no text, which is every one of them but a pie with its labels on.
    /// </remarks>
    public virtual void TakeLabelInk(IBrush ink) => _ = ink;

    /// <summary>Takes the ring of the circle a chart has put this series in, as fractions of the radius.</summary>
    /// <param name="from">Where the ring starts, nought at the middle of the circle.</param>
    /// <param name="to">Where it ends, one at the farthest the circle reaches.</param>
    /// <remarks>
    /// A chart of one pie gives it the whole circle and this does nothing. A chart of several - which is how
    /// nested pies are drawn - divides the circle between them, because two pies drawn in the same ring would
    /// simply land on each other. It is a hand-over rather than a property for the same reason the palette is:
    /// only a chart knows how many series it holds. Series that are not pies have nothing to do with it.
    /// </remarks>
    public virtual void TakeBand(double from, double to)
    {
        _ = from;
        _ = to;
    }

    /// <summary>
    /// The records the series is drawing, lazily: what is in view, after sampling.
    /// </summary>
    /// <remarks>
    /// A view rather than a list, and a pair of a series and a position rather than a copy of the values, so a
    /// chart of a million points walks them without making a million of anything. See <see cref="ChartItem"/>.
    /// </remarks>
    public virtual IEnumerable<ChartItem> Items => [];

    /// <summary>The nearest record the series draws within reach of a point, or null when none is.</summary>
    /// <param name="point">The position, in the chart's own coordinates.</param>
    /// <param name="sensitivity">How far the pointer may be from the record, in logical pixels.</param>
    /// <returns>The hit, or null.</returns>
    /// <remarks>
    /// The hook the extension contract in the design brief names: a series that draws something other than a
    /// line overrides this to say what its ink gets hit, and everything built on it - the chart's own
    /// <c>FindDataPoints</c> - follows. Records with no value are never hits, because there is nothing of them to
    /// point at.
    /// </remarks>
    public virtual HitData? HitTestItem(Point point, double sensitivity) => null;

    /// <summary>Every record the series draws that falls inside a rectangle.</summary>
    /// <param name="region">The rectangle, in the chart's own coordinates.</param>
    /// <returns>The records, in data order.</returns>
    public virtual IReadOnlyList<ChartItem> GetItemsInRegion(Rect region) => [];

    /// <summary>The unit value of a record on a dimension, or NaN when it has none.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space.</returns>
    internal virtual double UnitAt(ChartDimension dimension, int index) => double.NaN;

    /// <summary>The caller's object behind a record, when the series can still find it.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The object, or null.</returns>
    internal virtual object? SourceItemAt(int index) => null;

    /// <summary>Where a record is drawn, when it is drawn at all.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The position, or null.</returns>
    /// <remarks>
    /// The series answers this rather than the chart because a series may be measured against an axis of its own,
    /// and the chart's own mapping knows nothing about that axis.
    /// </remarks>
    internal virtual Point? PositionOf(int index) => null;

    /// <summary>
    /// Asks for a repaint, and tells the transform that the ranges it worked out may no longer be right.
    /// </summary>
    protected void Invalidate()
    {
        Transform?.InvalidateRange();
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks for a repaint, without saying that what the axes cover has changed.</summary>
    /// <remarks>
    /// What a change of ink is: the colours a series is drawn in, rather than the readings it drew them from. The
    /// geometry of a line is a shape in pixels and the pen is put on at draw time, so nothing about it has to be
    /// worked out again - and saying that the ranges moved as well is what would work it out.
    /// </remarks>
    private protected void Repaint() => Invalidated?.Invoke(this, EventArgs.Empty);

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
        // A hidden series asks the axes for nothing, whatever kind of series it is - which is the whole of "a
        // hidden series takes no part in the ranges the axes cover". It is asked here rather than in an override
        // because every series type overrides DescribeData, and this is the one place they all come through.
        if (!Visible)
        {
            return;
        }

        EnsureData();
        DescribeData(description);
    }

    void IChartElement.MappingChanged() => _geometryIsStale = true;
}
