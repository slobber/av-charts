using Avalonia;

namespace AvCharts.Charts;

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
    private readonly Dictionary<ChartDimension, AxisLayout> _layouts = [];
    private readonly Dictionary<ChartDimension, List<BoundedValue>> _bounds = [];
    private readonly List<IChartElement> _elements = [];

    private Rect _plotBounds;
    private bool _rangesAreStale = true;
    private bool _mappingIsStale = true;

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
    /// Gets what the axis of a dimension worked out for this chart: the range on screen and where its grid
    /// lines fall. The answer belongs to this transform rather than to the axis, so an axis shared between
    /// several charts gives each of them its own.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The layout, or <see cref="AxisLayout.Empty"/> when the dimension has no axis.</returns>
    public AxisLayout GetLayout(ChartDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        Update();

        return _layouts.GetValueOrDefault(dimension) ?? AxisLayout.Empty;
    }

    /// <summary>Gives a dimension an axis, or takes it away with <see langword="null"/>.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="axis">The axis, or null to remove the one in place.</param>
    public void SetAxis(ChartDimension dimension, IAxis? axis)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        if (axis is null)
        {
            if (_axes.Remove(dimension))
            {
                _layouts.Remove(dimension);
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

        if (element is Series series)
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
            if (element is Series series)
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
    public bool TryValueToPoint(ChartValue x, ChartValue y, out Point point)
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
    /// Maps a pair of unit values onto a point, which is what a series does with the values it kept.
    /// </summary>
    /// <param name="x">The value on the horizontal dimension, in unit space.</param>
    /// <param name="y">The value on the vertical dimension, in unit space.</param>
    /// <param name="point">The position in pixels, when this returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="false"/> when a dimension has no axis, when the plot has no area, or when either value is
    /// <see cref="double.NaN"/> - which is what a series stores where a record has no value.
    /// </returns>
    public bool TryUnitToPoint(double x, double y, out Point point)
    {
        point = default;

        Update();

        if (GetAxis(ChartDimension.X) is null || GetAxis(ChartDimension.Y) is null ||
            !(_plotBounds.Width > 0) || !(_plotBounds.Height > 0) ||
            double.IsNaN(x) || double.IsNaN(y))
        {
            return false;
        }

        var horizontal = GetLayout(ChartDimension.X).UnitRange.Normalize(x);
        var vertical = GetLayout(ChartDimension.Y).UnitRange.Normalize(y);

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
    public bool TryPointToValue(Point point, out ChartValue x, out ChartValue y)
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

        var description = new DataDescription(_bounds);

        foreach (var element in _elements.ToArray())
        {
            element.DescribeData(description);
        }

        foreach (var (dimension, axis) in _axes)
        {
            IReadOnlyList<BoundedValue> values = _bounds.TryGetValue(dimension, out var described) ? described : [];
            _layouts[dimension] = axis.Resolve(values, GetDimensionLength(dimension));
        }
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
        if (axis is AxisBase configurable)
        {
            configurable.Changed += OnAxisChanged;
        }
    }

    private void Unhook(IAxis axis)
    {
        if (axis is AxisBase configurable)
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
