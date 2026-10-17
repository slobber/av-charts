using Avalonia;
using Avalonia.Media;
using AvCharts.Data;

namespace AvCharts.Series;

/// <summary>
/// Readings drawn as a grid of coloured cells: one cell for each pair of horizontal and vertical positions.
/// </summary>
/// <remarks>
/// <para>
/// The two selectors place a record on the chart and the value selector colours it. Cells meet at the midpoint
/// between neighbouring positions, and the outermost cell reaches half a step beyond the outermost reading, so
/// the grid tiles the plot without leaving a frame of empty pixels around it.
/// </para>
/// <para>
/// The third reading is drawn rather than measured by an axis. It has no axis of its own because a heat map's
/// colour is a scale, not a second coordinate: the range of colours is the range of the values, unless the
/// caller pins it with <see cref="Minimum"/> and <see cref="Maximum"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class HeatSeries<T> : CartesianSeries<T>
{
    private Func<T, double>? _valueSelector;
    private HeatColorScale _colorScale = new(
        Avalonia.Media.Color.Parse("#173A7A"),
        Avalonia.Media.Color.Parse("#30A46C"),
        Avalonia.Media.Color.Parse("#F2D94E"),
        Avalonia.Media.Color.Parse("#E4572E"));
    private double? _minimum;
    private double? _maximum;
    private Pen? _stroke;
    private Cell[] _cells = [];
    private Fill[] _fills = [];

    /// <summary>How to read the value that colours a cell; a missing value leaves the cell unpainted.</summary>
    public Func<T, double>? ValueSelector
    {
        get => _valueSelector;
        set
        {
            if (ReferenceEquals(_valueSelector, value))
            {
                return;
            }

            _valueSelector = value;
            InvalidateGeometry();
        }
    }

    /// <summary>Where the value that colours a cell is written, as a path; for a markup file.</summary>
    public string? ValuePath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            ValueSelector = Paths.Number<T>(value);
        }
    }

    /// <summary>The colours cells move through; blue through green, yellow and red by default.</summary>
    public HeatColorScale ColorScale
    {
        get => _colorScale;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_colorScale, value))
            {
                return;
            }

            _colorScale = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The value that takes the first colour; unset, the lowest value in the series.</summary>
    public double? Minimum
    {
        get => _minimum;
        set
        {
            if (Nullable.Equals(_minimum, value))
            {
                return;
            }

            _minimum = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The value that takes the last colour; unset, the highest value in the series.</summary>
    public double? Maximum
    {
        get => _maximum;
        set
        {
            if (Nullable.Equals(_maximum, value))
            {
                return;
            }

            _maximum = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The pen round each cell; without one, cells meet without a drawn edge.</summary>
    public Pen? Stroke
    {
        get => _stroke;
        set
        {
            if (ReferenceEquals(_stroke, value))
            {
                return;
            }

            _stroke = value;
            InvalidateGeometry();
        }
    }

    /// <inheritdoc />
    public override bool HasOwnColours => true;

    /// <inheritdoc />
    protected override void DescribeData(DataDescription description)
    {
        base.DescribeData(description);

        var x = Positions(XValues);
        var y = Positions(YValues);

        if (x.Boundaries.Length > 1)
        {
            description.Add(ChartDimension.X, x.Boundaries[0]);
            description.Add(ChartDimension.X, x.Boundaries[^1]);
        }

        if (y.Boundaries.Length > 1)
        {
            description.Add(ChartDimension.Y, y.Boundaries[0]);
            description.Add(ChartDimension.Y, y.Boundaries[^1]);
        }
    }

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var x = Positions(XValues);
        var y = Positions(YValues);
        var cells = new List<Cell>();
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;

        for (var index = 0; index < _count; index++)
        {
            var value = ValueOf(index);
            var left = Array.BinarySearch(x.Values, XValues[index]);
            var bottom = Array.BinarySearch(y.Values, YValues[index]);

            if (double.IsNaN(value) || left < 0 || bottom < 0 ||
                !TryCell(context, x.Boundaries, y.Boundaries, left, bottom, out var bounds))
            {
                continue;
            }

            cells.Add(new Cell(index, bounds, value));
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        _cells = [.. cells];

        if (_cells.Length == 0)
        {
            _fills = [];
            return;
        }

        var low = _minimum ?? minimum;
        var high = _maximum ?? maximum;
        var fills = new Dictionary<Color, List<Rect>>();

        foreach (var cell in _cells)
        {
            var color = _colorScale.ColorAt(cell.Value, low, high);

            if (!fills.TryGetValue(color, out var rects))
            {
                rects = [];
                fills[color] = rects;
            }

            rects.Add(cell.Bounds);
        }

        _fills = [.. fills.Select(pair => new Fill(new SolidColorBrush(pair.Key), [.. pair.Value]))];
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        foreach (var fill in _fills)
        {
            foreach (var rect in fill.Rects)
            {
                context.FillRectangle(fill.Brush, rect);

                if (_stroke is not null)
                {
                    context.DrawRectangle(null, _stroke, rect);
                }
            }
        }
    }

    /// <inheritdoc />
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        foreach (var cell in _cells)
        {
            if (cell.Bounds.Contains(point))
            {
                return new HitData
                {
                    Distance = 0,
                    ChartItem = new ChartItem(this, cell.Index),
                    DisplayText = Format(cell.Value),
                };
            }
        }

        _ = sensitivity;
        return null;
    }

    /// <inheritdoc />
    public override IReadOnlyList<ChartItem> GetItemsInRegion(Rect region) =>
        [.. _cells.Where(cell => cell.Bounds.Intersects(region)).Select(cell => new ChartItem(this, cell.Index))];

    private static bool TryCell(
        in SeriesRenderContext context,
        double[] x,
        double[] y,
        int column,
        int row,
        out Rect bounds)
    {
        bounds = default;

        if (!context.TryUnitPoint(x[column], y[row], out var lowerLeft) ||
            !context.TryUnitPoint(x[column + 1], y[row + 1], out var upperRight))
        {
            return false;
        }

        var left = Math.Min(lowerLeft.X, upperRight.X);
        var top = Math.Min(lowerLeft.Y, upperRight.Y);
        var right = Math.Max(lowerLeft.X, upperRight.X);
        var bottom = Math.Max(lowerLeft.Y, upperRight.Y);

        if (!(right > left) || !(bottom > top))
        {
            return false;
        }

        bounds = new Rect(left, top, right - left, bottom - top);
        return true;
    }

    private double ValueOf(int index)
    {
        if (_valueSelector is null || index < 0 || index >= _count || _items[index] is not { } item)
        {
            return double.NaN;
        }

        var value = _valueSelector(item);
        return double.IsNaN(value) || double.IsInfinity(value) ? double.NaN : value;
    }

    private static string Format(double value) => value.ToString("0.###");

    private static CellPositions Positions(ReadOnlySpan<double> values)
    {
        var distinct = new List<double>();

        foreach (var value in values)
        {
            if (!double.IsNaN(value) && !distinct.Contains(value))
            {
                distinct.Add(value);
            }
        }

        if (distinct.Count == 0)
        {
            return new CellPositions([], []);
        }

        distinct.Sort();
        var boundaries = new double[distinct.Count + 1];

        if (distinct.Count == 1)
        {
            boundaries[0] = distinct[0] - 0.5;
            boundaries[1] = distinct[0] + 0.5;
            return new CellPositions([.. distinct], boundaries);
        }

        boundaries[0] = distinct[0] - ((distinct[1] - distinct[0]) / 2);

        for (var index = 1; index < distinct.Count; index++)
        {
            boundaries[index] = (distinct[index - 1] + distinct[index]) / 2;
        }

        boundaries[^1] = distinct[^1] + ((distinct[^1] - distinct[^2]) / 2);
        return new CellPositions([.. distinct], boundaries);
    }

    private readonly record struct CellPositions(double[] Values, double[] Boundaries);

    private readonly record struct Cell(int Index, Rect Bounds, double Value);

    private readonly record struct Fill(SolidColorBrush Brush, Rect[] Rects);
}
