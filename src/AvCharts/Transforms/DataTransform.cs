using Avalonia;
using AvCharts.Axes;
using AvCharts.Data;
using AvCharts.Elements;
using AvCharts.Series;

namespace AvCharts.Transforms;

/// <summary>
/// Ties elements to axes and maps their values onto screen coordinates, and back again.
/// </summary>
/// <remarks>
/// <para>
/// This is the class the whole library leans on. A series never works out where to draw itself: it converts
/// its values through <see cref="ChartValue"/>, asks the transform for positions, and gets told when the
/// mapping moved.
/// </para>
/// <para>
/// Everything below the transform works in unit space (see <see cref="IAxis"/>), everything above works in
/// data space. The transform is the only place that knows about both, which is why hit testing, sampling and
/// stacking can stay axis-agnostic.
/// </para>
/// <para>
/// Updates are lazy and idempotent: changing a plot rectangle or a value only marks the transform stale, and
/// the next question asked of it - or the chart's own layout pass - brings it up to date once.
/// </para>
/// </remarks>
public class DataTransform
{
    private readonly Dictionary<ChartDimension, IAxis> _axes = [];
    private readonly Dictionary<IAxis, AxisLayout> _layouts = [];
    private readonly Dictionary<IAxis, (ChartDimension Dimension, List<BoundedValue> Bounds)> _bounds = [];
    private readonly Dictionary<IAxis, ChartDimension> _dimensions = [];
    private readonly HashSet<IAxis> _hooked = [];
    private readonly List<IChartElement> _elements = [];

    private Rect _plotBounds;
    private bool _rangesAreStale = true;
    private bool _mappingIsStale = true;

    // The mapping, kept as fields rather than looked up: a series places a value at a time, so the two unit
    // ranges behind TryUnitToPoint are asked for once per value, and asking the dictionaries for them was the
    // whole of what one placed value cost. Refreshed by Update, which is what says the mapping moved.
    private AxisUnitRange _mapX = AxisUnitRange.Empty;
    private AxisUnitRange _mapY = AxisUnitRange.Empty;
    private bool _mapHasAxes;

    /// <summary>The rectangle the data is mapped into, in pixels.</summary>
    public Rect PlotBounds
    {
        get => _plotBounds;
        set
        {
            if (_plotBounds == value)
            {
                return;
            }

            _plotBounds = value;
            InvalidateMapping();
        }
    }

    /// <summary>The axes in play, by dimension.</summary>
    public IReadOnlyDictionary<ChartDimension, IAxis> Axes => _axes;

    /// <summary>The elements that describe their data to this transform.</summary>
    public IReadOnlyList<IChartElement> Elements => _elements;

    /// <summary>Gets the axis of a dimension, or null when the dimension has none.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The axis, or null.</returns>
    public IAxis? GetAxis(ChartDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        return _axes.GetValueOrDefault(dimension);
    }

    /// <summary>
    /// Every axis this transform resolves: the chart's own, and any a series brought of its own.
    /// </summary>
    /// <remarks>
    /// The answer is only complete once the elements have been described, which <see cref="Update"/> does, so
    /// asking brings the transform up to date first.
    /// </remarks>
    public IReadOnlyList<IAxis> AxesInPlay
    {
        get
        {
            Update();
            return [.. _dimensions.Keys];
        }
    }

    /// <summary>
    /// Gets what the axis of a dimension worked out for this chart: the range on screen and where its grid
    /// lines fall. The answer belongs to this transform rather than to the axis, so an axis shared between
    /// several charts gives each of them its own.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The layout, or <see cref="AxisLayout.Empty"/> when the dimension has no axis.</returns>
    public AxisLayout GetLayout(ChartDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        return GetAxis(dimension) is { } axis ? GetLayout(axis) : AxisLayout.Empty;
    }

    /// <summary>Gets what one axis worked out for this chart.</summary>
    /// <param name="axis">The axis; one that no element is measured against has no layout.</param>
    /// <returns>The layout, or <see cref="AxisLayout.Empty"/> when this chart does not use the axis.</returns>
    public AxisLayout GetLayout(IAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        Update();

        return _layouts.GetValueOrDefault(axis) ?? AxisLayout.Empty;
    }

    /// <summary>Gets the stretch of unit space the elements registered here cover on one axis.</summary>
    /// <param name="axis">The axis.</param>
    /// <returns>
    /// The range, in unit space, or <see langword="null"/> when nothing has declared a value to that axis.
    /// </returns>
    /// <remarks>
    /// <para>
    /// What the readings reach, rather than what the axis shows: an axis with a window of its own on it - a fixed
    /// scale, a zoomed one - covers a part of this, and something that says where the window is inside the
    /// readings needs both numbers. <see cref="GetLayout(IAxis)"/> is the other one.
    /// </para>
    /// <para>
    /// The margins of a declared value count, since a column reaches half a slot past the value it stands at: the
    /// range is what is drawn, not what is plotted.
    /// </para>
    /// </remarks>
    public AxisUnitRange? DataRange(IAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        Update();

        if (!_bounds.TryGetValue(axis, out var entry) || entry.Bounds.Count == 0)
        {
            return null;
        }

        var minimum = double.NaN;
        var maximum = double.NaN;

        foreach (var bound in entry.Bounds)
        {
            minimum = double.IsNaN(minimum) ? bound.Minimum : Math.Min(minimum, bound.Minimum);
            maximum = double.IsNaN(maximum) ? bound.Maximum : Math.Max(maximum, bound.Maximum);
        }

        return double.IsNaN(minimum) ? null : new AxisUnitRange(minimum, maximum);
    }

    /// <summary>Gives a dimension an axis, or takes it away with <see langword="null"/>.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="axis">The axis, or null to remove the one in place.</param>
    public void SetAxis(ChartDimension dimension, IAxis? axis)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        if (axis is null)
        {
            if (_axes.Remove(dimension, out var removed))
            {
                Unhook(removed);
                InvalidateRange();
            }

            return;
        }

        if (_axes.TryGetValue(dimension, out var existing))
        {
            if (ReferenceEquals(existing, axis))
            {
                return;
            }

            Unhook(existing);
        }

        _axes[dimension] = axis;
        Hook(axis);
        InvalidateRange();
    }

    /// <summary>Starts describing an element's data.</summary>
    /// <param name="element">The element; adding the same one twice does nothing.</param>
    public void AddElement(IChartElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_elements.Contains(element))
        {
            return;
        }

        _elements.Add(element);

        if (element is SeriesBase series)
        {
            series.Transform = this;
        }

        InvalidateRange();
    }

    /// <summary>Stops describing an element's data.</summary>
    /// <param name="element">The element.</param>
    public void RemoveElement(IChartElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_elements.Remove(element))
        {
            if (element is SeriesBase series)
            {
                series.Transform = null;
            }

            InvalidateRange();
        }
    }

    /// <summary>
    /// Marks the value ranges stale: the data, the elements or the axes changed, so the axes have to be told
    /// what to cover again.
    /// </summary>
    public void InvalidateRange()
    {
        _rangesAreStale = true;
        InvalidateMapping();
    }

    /// <summary>Marks the mapping stale: the ranges are fine, the rectangle they map into is not.</summary>
    public void InvalidateMapping() => _mappingIsStale = true;

    /// <summary>
    /// Brings the transform up to date: describes every element's data to the axes if the ranges are stale,
    /// then tells the elements once that the mapping moved.
    /// </summary>
    /// <remarks>The chart calls this during layout; the mapping queries call it too, so it is never required.</remarks>
    public void Update()
    {
        if (_rangesAreStale)
        {
            _rangesAreStale = false;
            UpdateRanges();
            _mappingIsStale = true;
        }

        if (!_mappingIsStale)
        {
            return;
        }

        _mappingIsStale = false;

        // Kept before the elements are told, and before anything they do in reaction can mark it stale again:
        // what is cached is the mapping this update worked out.
        _mapX = GetLayout(ChartDimension.X).UnitRange;
        _mapY = GetLayout(ChartDimension.Y).UnitRange;
        _mapHasAxes = GetAxis(ChartDimension.X) is not null && GetAxis(ChartDimension.Y) is not null;

        // A snapshot, because an element may register or drop elements while reacting to the move.
        foreach (var element in _elements.ToArray())
        {
            element.MappingChanged();
        }
    }

    /// <summary>Maps a pair of values onto a point, when both dimensions are ready to be mapped.</summary>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <param name="point">The position in pixels, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when a dimension is missing or either value is missing.</returns>
    /// <remarks>
    /// Virtual, because this and the two below are where a coordinate system that is not "across and up" lives:
    /// the polar mapping a pie chart needs, the hexagonal one a capability chart needs. Handing a chart a
    /// transform of one's own is the only way to use one, and these are the three things it has to answer.
    /// </remarks>
    public virtual bool TryValueToPoint(ChartValue x, ChartValue y, out Point point)
    {
        point = default;

        if (!TryNormalize(ChartDimension.X, x, out var horizontal) ||
            !TryNormalize(ChartDimension.Y, y, out var vertical))
        {
            return false;
        }

        point = new Point(
            _plotBounds.X + (horizontal * _plotBounds.Width),
            _plotBounds.Bottom - (vertical * _plotBounds.Height));

        return true;
    }

    /// <summary>
    /// Maps one value on one dimension onto its position across or up the plot, which is what a band between two
    /// readings - a section of the plot, a section of the axis, a line at a value - is drawn from.
    /// </summary>
    /// <param name="dimension">Which dimension the value belongs to.</param>
    /// <param name="value">The value.</param>
    /// <param name="position">The position in pixels, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when that dimension has no axis, or when the axis cannot place the value.</returns>
    /// <remarks>
    /// The single-dimension half of <see cref="TryValueToPoint"/>, and the reason it exists: an annotation that
    /// runs the whole way across the plot has one value to place and not two, and asking it for a pair would mean
    /// inventing the other one.
    /// </remarks>
    public virtual bool TryValueToPosition(ChartDimension dimension, ChartValue value, out double position)
    {
        position = 0;

        var layout = GetLayout(dimension);
        var axis = GetAxis(dimension);

        if (axis is null || value.IsEmpty || !axis.TryToUnit(value, out var unit))
        {
            return false;
        }

        var fraction = layout.UnitRange.Normalize(unit);

        position = dimension == ChartDimension.X
            ? _plotBounds.X + (fraction * _plotBounds.Width)
            : _plotBounds.Bottom - (fraction * _plotBounds.Height);

        return true;
    }

    /// <summary>
    /// Maps a pair of unit values onto a point, which is what a series does with the values it kept.
    /// </summary>
    /// <param name="x">The value on the horizontal dimension, in unit space.</param>
    /// <param name="y">The value on the vertical dimension, in unit space.</param>
    /// <param name="point">The position in pixels, when this returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="false"/> when a dimension has no axis, when the plot has no area, or when either value is
    /// <see cref="double.NaN"/> - which is what a series stores where a record has no value.
    /// </returns>
    public virtual bool TryUnitToPoint(double x, double y, out Point point)
    {
        point = default;

        Update();

        if (!_mapHasAxes || !(_plotBounds.Width > 0) || !(_plotBounds.Height > 0) ||
            double.IsNaN(x) || double.IsNaN(y))
        {
            return false;
        }

        var horizontal = _mapX.Normalize(x);
        var vertical = _mapY.Normalize(y);

        point = new Point(
            _plotBounds.X + (horizontal * _plotBounds.Width),
            _plotBounds.Bottom - (vertical * _plotBounds.Height));

        return true;
    }

    /// <summary>Maps a point back onto a pair of values: what a hit test, a crosshair or a data tip starts with.</summary>
    /// <param name="point">The position in pixels.</param>
    /// <param name="x">The value on the horizontal dimension, when this returns <see langword="true"/>.</param>
    /// <param name="y">The value on the vertical dimension, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when a dimension has no axis, or the plot has no area.</returns>
    public virtual bool TryPointToValue(Point point, out ChartValue x, out ChartValue y)
    {
        x = ChartValue.Empty;
        y = ChartValue.Empty;

        Update();

        var horizontalAxis = GetAxis(ChartDimension.X);
        var verticalAxis = GetAxis(ChartDimension.Y);

        if (horizontalAxis is null || verticalAxis is null ||
            !(_plotBounds.Width > 0) || !(_plotBounds.Height > 0))
        {
            return false;
        }

        var horizontal = (point.X - _plotBounds.X) / _plotBounds.Width;
        var vertical = (_plotBounds.Bottom - point.Y) / _plotBounds.Height;

        x = horizontalAxis.FromUnit(GetLayout(ChartDimension.X).UnitRange.Denormalize(horizontal));
        y = verticalAxis.FromUnit(GetLayout(ChartDimension.Y).UnitRange.Denormalize(vertical));

        return true;
    }

    /// <summary>Describes every element's data and hands the result to the axis of each dimension.</summary>
    protected virtual void UpdateRanges()
    {
        _bounds.Clear();
        _dimensions.Clear();
        _layouts.Clear();

        foreach (var element in _elements.ToArray())
        {
            // Each element describes its values to its own axes, which are the chart's unless the element brought
            // one - and the element itself says nothing about it: it goes on adding values per dimension.
            var axes = AxesFor(element);

            foreach (var (dimension, axis) in axes)
            {
                _dimensions[axis] = dimension;
            }

            element.DescribeData(new DataDescription(_bounds, axes));
        }

        // The chart's own axes are in play whether or not anything is measured against them: the chart's own
        // coordinates are theirs, and a chart with no data still has a scale to draw.
        foreach (var (dimension, axis) in _axes)
        {
            _dimensions[axis] = dimension;
        }

        foreach (var (axis, dimension) in _dimensions)
        {
            IReadOnlyList<BoundedValue> values = _bounds.TryGetValue(axis, out var described) ? described.Bounds : [];
            _layouts[axis] = axis.Resolve(values, GetDimensionLength(dimension));
        }

        // An axis nobody is measured against is no longer this chart's business, including its settings.
        foreach (var axis in _hooked.ToArray())
        {
            if (!_dimensions.ContainsKey(axis))
            {
                Unhook(axis);
                _hooked.Remove(axis);
            }
        }
    }

    /// <summary>The axes an element is measured against: its own where it named one, the chart's otherwise.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The axes, by dimension.</returns>
    protected virtual IReadOnlyDictionary<ChartDimension, IAxis> AxesFor(IChartElement element)
    {
        var axes = new Dictionary<ChartDimension, IAxis>();

        foreach (var (dimension, axis) in _axes)
        {
            axes[dimension] = element is SeriesBase series ? series.AxisOn(dimension) ?? axis : axis;
        }

        if (element is SeriesBase withOwn)
        {
            foreach (var dimension in new[] { ChartDimension.X, ChartDimension.Y })
            {
                if (withOwn.AxisOn(dimension) is { } own)
                {
                    axes[dimension] = own;
                    Hook(own);
                }
            }
        }

        return axes;
    }

    /// <summary>
    /// How long a dimension is on screen. The Cartesian pair is the obvious answer; a polar transform
    /// overrides this for radius and angle.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The length in pixels, or 0 when this transform has nothing to say about the dimension.</returns>
    protected virtual double GetDimensionLength(ChartDimension dimension)
    {
        if (dimension == ChartDimension.X)
        {
            return _plotBounds.Width;
        }

        return dimension == ChartDimension.Y ? _plotBounds.Height : 0;
    }

    private void Hook(IAxis axis)
    {
        if (axis is AxisBase configurable && _hooked.Add(axis))
        {
            configurable.Changed += OnAxisChanged;
        }
    }

    private void Unhook(IAxis axis)
    {
        if (axis is AxisBase configurable && _hooked.Remove(axis))
        {
            configurable.Changed -= OnAxisChanged;
        }
    }

    private void OnAxisChanged(object? sender, EventArgs e) => InvalidateRange();

    private bool TryNormalize(ChartDimension dimension, ChartValue value, out double fraction)
    {
        fraction = 0.5;

        Update();

        var axis = GetAxis(dimension);

        if (axis is null || value.IsEmpty)
        {
            return false;
        }

        if (!axis.TryToUnit(value, out var unit))
        {
            return false;
        }

        fraction = GetLayout(dimension).UnitRange.Normalize(unit);

        return true;
    }
}
