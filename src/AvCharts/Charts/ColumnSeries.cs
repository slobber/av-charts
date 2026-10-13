using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// A series drawn as columns: one bar per record, standing in the slot its horizontal value names.
/// </summary>
/// <remarks>
/// <para>
/// The shape of a column is a <em>band</em>: the space one unit of the band axis covers, which is a whole
/// category slot on a <see cref="CategoryAxis"/> and one unit of whatever the axis measures otherwise. That is
/// Flex's unit size, arrived at from the layout instead of stored on the axis - an axis here holds settings and
/// not results, and one axis can drive several charts of different widths.
/// </para>
/// <para>
/// Where a column stands within its band is the chart's decision rather than the series': see
/// <see cref="BandLayout"/>. A series that is not in a chart, or is in one with no band layout, takes the whole
/// band.
/// </para>
/// <para>
/// A record with no value keeps its slot and draws nothing. Dropping it would move every column after it and
/// turn the category axis into a lie about where the data sits, which is the column half of the null policy in
/// the design brief.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class ColumnSeries<T> : CartesianSeries<T>, IColumn, IStackable
{
    /// <summary>How much of a band a column takes when it has the band to itself; Flex's own default.</summary>
    public const double DefaultColumnWidthRatio = 0.65;

    private double _columnWidthRatio = DefaultColumnWidthRatio;
    private double? _maxColumnWidth;
    private IBrush? _fill;
    private Pen? _stroke;
    private Geometry? _bars;
    private double[]? _bases;

    /// <summary>How much of the band between two slots a column takes, as a fraction; 0.65 by default.</summary>
    /// <remarks>
    /// What is actually drawn is the smaller of this fraction of the band and <see cref="MaxColumnWidth"/>, which
    /// is Flex's rule: the fraction keeps a column in proportion to its slot as a chart is resized, and the cap
    /// stops a chart of three categories from drawing three absurdly wide columns.
    /// </remarks>
    public double ColumnWidthRatio
    {
        get => _columnWidthRatio;
        set
        {
            if (!(value > 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A column is a fraction of its band, so the fraction has to be above zero.");
            }

            if (_columnWidthRatio.Equals(value))
            {
                return;
            }

            _columnWidthRatio = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The widest a column may be drawn, in logical pixels; unset, only the band limits it.</summary>
    public double? MaxColumnWidth
    {
        get => _maxColumnWidth;
        set
        {
            if (value is { } cap && !(cap > 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A cap of zero or less would draw nothing at all, which is a mistake rather than a setting.");
            }

            if (Nullable.Equals(_maxColumnWidth, value))
            {
                return;
            }

            _maxColumnWidth = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The colour a column is filled with; without one, the chart's palette fills it.</summary>
    public IBrush? Fill
    {
        get => _fill;
        set
        {
            if (ReferenceEquals(_fill, value))
            {
                return;
            }

            _fill = value;
            Invalidate();
        }
    }

    /// <summary>The pen around a column; without one, a column has no edge.</summary>
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
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override bool HasOwnColours => Fill is not null || Stroke is not null;

    /// <inheritdoc />
    public override IBrush? Color => Fill ?? base.Color;

    /// <summary>Which dimension the bands run along: horizontal for columns, vertical for bars.</summary>
    protected virtual ChartDimension BandDimension => ChartDimension.X;

    /// <summary>Which dimension the values run along: the other one.</summary>
    protected ChartDimension ValueDimension =>
        BandDimension == ChartDimension.X ? ChartDimension.Y : ChartDimension.X;

    /// <summary>
    /// The unit value a column stands on: the axis' zero, or the low end of what the axis covers when it has no
    /// zero to stand on.
    /// </summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    /// <returns>The base, in unit space.</returns>
    protected double BaseUnit(int index, in SeriesRenderContext context)
    {
        if (_bases is { } bases && index >= 0 && index < bases.Length)
        {
            return bases[index];
        }

        var zero = ZeroOn(ValueDimension);

        return double.IsNaN(zero) ? RangeOf(ValueDimension).Minimum : zero;
    }

    /// <inheritdoc />
    public void Stack(StackTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);
        EnsureData();

        // The base of every column, worked out in one pass: what the series below have reached by the time this
        // record's own value is added. Allocating the array once and keeping it is what makes a stack cost one
        // pass over the values rather than one per frame.
        if (_bases is null || _bases.Length != _count)
        {
            _bases = new double[_count];
        }

        var zero = ZeroOn(ValueDimension);
        var baseUnit = double.IsNaN(zero) ? 0 : zero;
        var vertical = BandDimension == ChartDimension.Y;

        for (var index = 0; index < _count; index++)
        {
            var band = vertical ? _y[index] : _x[index];
            var value = vertical ? _x[index] : _y[index];

            if (double.IsNaN(band) || double.IsNaN(value))
            {
                // A record with no value takes no height and adds nothing, but the slot it is in is where it is:
                // the stack above it starts from what is already there.
                _bases[index] = baseUnit;
                continue;
            }

            var height = value - baseUnit;
            _bases[index] = baseUnit + (height >= 0 ? totals.Above(band) : totals.Below(band));
            totals.Add(band, height);
        }

        InvalidateGeometry();
    }

    /// <inheritdoc />
    protected override void DescribeData(DataDescription description)
    {
        var transform = Transform;

        if (AxisFor(BandDimension) is null)
        {
            return;
        }

        EnsureData();

        var x = XValues;
        var y = YValues;
        var vertical = BandDimension == ChartDimension.Y;
        var zero = ZeroOn(ValueDimension);

        for (var index = 0; index < x.Length; index++)
        {
            var band = vertical ? y[index] : x[index];
            var value = vertical ? x[index] : y[index];

            // The slot is declared whether or not the record has a value: a column that cannot be drawn still
            // sits in its place, and the axis still knows the category is there. Half a slot of room on each
            // side is what keeps the first and last columns from being cut in half.
            if (!double.IsNaN(band))
            {
                description.Add(BandDimension, new BoundedValue(band, 0.5, 0.5));
            }

            if (AxisFor(ValueDimension) is null || double.IsNaN(value))
            {
                continue;
            }

            if (double.IsNaN(zero))
            {
                // An axis with no zero to stand on - a logarithmic one - gets the value and nothing else.
                description.Add(ValueDimension, value);
                continue;
            }

            // A column stands on its base and reaches its own height above it, so the axis has to cover both -
            // which is what makes a chart of values far from zero show bars rather than spikes off the bottom of
            // the plot, and what makes a stack's totals the thing the axis grows to.
            var baseUnit = _bases is { } bases && index < bases.Length ? bases[index] : zero;

            description.Add(ValueDimension, baseUnit);
            description.Add(ValueDimension, baseUnit + (value - zero));
        }
    }

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var batch = new GeometryBatch();
        var plot = context.PlotBounds;
        var vertical = BandDimension == ChartDimension.Y;
        var layout = context.Transform.GetLayout(AxisFor(BandDimension)!);
        var length = vertical ? plot.Height : plot.Width;

        if (layout.UnitRange.IsEmpty || !(length > 0))
        {
            _bars = batch.Build();
            return;
        }

        var (slot, slots) = context.Bands?.PlaceOf(this) ?? (0, 1);

        // The band is one unit of the axis, and this series gets its share of the fraction of it a column takes.
        var band = length / layout.UnitRange.Length * ColumnWidthRatio;
        var width = band / slots;

        if (MaxColumnWidth is { } cap && width > cap)
        {
            width = cap;
        }

        if (!(width > 0))
        {
            _bars = batch.Build();
            return;
        }

        // The cluster is centred on the slot, and this series stands in the middle of its own share of it.
        var shift = ((slot + 0.5) * width) - (band / 2);
        var zero = ZeroOn(ValueDimension);

        foreach (var (index, x, y, position) in PlacedRecords(context))
        {
            var baseUnit = BaseUnit(index, in context);
            var own = vertical ? x : y;

            // Where the column reaches: its own height above its base. Stacked that is above the total already
            // there, and standing on the axis alone it is the value itself - which is the same arithmetic with a
            // base of zero.
            var topUnit = double.IsNaN(zero) ? own : baseUnit + (own - zero);

            // Where the column stands and where it reaches: the same band position at each of the two values.
            var basePoint = vertical
                ? TryPoint(baseUnit, y, out var fromValue) ? fromValue : position
                : TryPoint(x, baseUnit, out var fromBase) ? fromBase : position;
            var topPoint = vertical
                ? TryPoint(topUnit, y, out var toValue) ? toValue : position
                : TryPoint(x, topUnit, out var toBase) ? toBase : position;

            var centre = (vertical ? position.Y : position.X) + shift;

            // The band edges are layout and are lined up with the pixel grid so that columns tile; the value end
            // is data and stays where the value put it.
            var near = context.Snap(centre - (width / 2));
            var far = context.Snap(centre + (width / 2));

            if (!(far > near))
            {
                continue;
            }

            var value = vertical ? topPoint.X : topPoint.Y;
            var edge = vertical ? basePoint.X : basePoint.Y;
            var start = Math.Min(value, edge);
            var size = Math.Abs(value - edge);

            batch.AddRectangle(vertical
                ? new Rect(start, near, size, far - near)
                : new Rect(near, start, far - near, size));
        }

        _bars = batch.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var fill = Fill ?? PaletteFill;

        if (_bars is not null && (fill is not null || Stroke is not null))
        {
            context.DrawGeometry(fill, Stroke, _bars);
        }
    }
}

/// <summary>
/// A series drawn as bars: <see cref="ColumnSeries{T}"/> lying on its side, with the bands running up the chart.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class BarSeries<T> : ColumnSeries<T>
{
    /// <inheritdoc />
    protected override ChartDimension BandDimension => ChartDimension.Y;
}
