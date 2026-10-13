using Avalonia.Media;

namespace AvCharts.Charts;

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

    /// <summary>Raised when the series changed and whatever draws it should repaint.</summary>
    public event EventHandler? Invalidated;

    /// <summary>The name shown in a legend or a data tip.</summary>
    public string DisplayName { get; set; } = string.Empty;

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
    protected void InvalidateData()
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
        if (!_dataIsStale)
        {
            return;
        }

        _dataIsStale = false;
        UpdateData();
    }

    void IChartElement.DescribeData(DataDescription description)
    {
        EnsureData();
        DescribeData(description);
    }

    void IChartElement.MappingChanged() => _geometryIsStale = true;
}
