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

    // What the last paint was told, so that a pointer can be answered without a paint of its own: where the
    // bands stand and how finely the chart is drawn. Both come from the chart during layout, and a hit test
    // happens between frames - so the answer is about the picture the reader is looking at, which is the one
    // thing a pointer can be about.
    private BandLayout? _bands;
    private double _scaling = 1;

    /// <summary>One column as it is drawn: the rectangle, and the end of it the reading is at.</summary>
    /// <param name="Rect">The rectangle, in the chart's own coordinates.</param>
    /// <param name="Value">The top of a column, or the far end of a bar: where the reading is drawn.</param>
    private readonly record struct ColumnShape(Rect Rect, Point Value);

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

        if (!TryBandFacts(in context, out var facts))
        {
            _bars = batch.Build();
            return;
        }

        foreach (var (index, x, y, position) in PlacedRecords(context))
        {
            if (TryColumn(index, x, y, position, in context, in facts, out var column))
            {
                batch.AddRectangle(column.Rect);
            }
        }

        _bars = batch.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        // What the columns were laid out against, kept: a pointer arrives between frames, and this is the
        // picture it is pointing at.
        _bands = renderContext.Bands;
        _scaling = renderContext.RenderScaling;

        var fill = Fill ?? PaletteFill;

        if (_bars is not null && (fill is not null || Stroke is not null))
        {
            context.DrawGeometry(fill, Stroke, _bars);
        }
    }

    /// <summary>
    /// The nearest record whose drawn column is within reach of a point.
    /// </summary>
    /// <param name="point">The position, in the chart's own coordinates.</param>
    /// <param name="sensitivity">How far the pointer may be from the column, in logical pixels.</param>
    /// <returns>The hit, or null.</returns>
    /// <remarks>
    /// <para>
    /// A column is not a point, and this is the hook the design brief's S1 contract is about: what a pointer on
    /// a chart of columns is on is the rectangle a record draws, not the place its value would be a mark at. The
    /// difference is the whole of the bug this was written for - side by side, two series' records stand at the
    /// same place in the middle of their slot, so a pointer anywhere on either column was within reach of
    /// neither; stacked, the reading of the series above is drawn higher up than its value, so its own column
    /// answered with the series below it.
    /// </para>
    /// <para>
    /// The columns are reached through the same index the readings are, so a chart of a hundred thousand columns
    /// still answers with a search rather than a walk.
    /// </para>
    /// </remarks>
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        EnsureData();

        if (!Visible || Transform is not { } transform || !(sensitivity > 0) || _count == 0)
        {
            return null;
        }

        var plot = transform.PlotBounds;
        var range = RangeOf(BandDimension);

        if (!(plot.Width > 0) || !(plot.Height > 0) || range.IsEmpty)
        {
            return null;
        }

        var context = ContextOf(transform);

        if (!TryBandFacts(in context, out var facts))
        {
            return null;
        }

        // Which records could be under the pointer. A column chart is indexed by the band axis' own order, so the
        // records in view are a search of the readings; a bar chart is indexed by value, so they are a walk.
        var (start, length) = BandDimension == ChartDimension.X
            ? Band(range.Minimum, range.Maximum)
            : (0, _count);

        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);
        HitData? best = null;

        for (var index = start; index < start + length; index++)
        {
            if (!Place(index, zeroX, zeroY, out var x, out var y) ||
                !TryPoint(x, y, out var position) ||
                !TryColumn(index, x, y, position, in context, in facts, out var column))
            {
                continue;
            }

            var distance = DistanceTo(column.Rect, point, plot);

            if (distance <= sensitivity && (best is null || distance < best.Value.Distance))
            {
                best = Hit(index, distance);
            }
        }

        return best;
    }

    /// <summary>Every record whose drawn column falls inside a rectangle.</summary>
    /// <param name="region">The rectangle, in the chart's own coordinates.</param>
    /// <returns>The records, in data order.</returns>
    public override IReadOnlyList<ChartItem> GetItemsInRegion(Rect region)
    {
        EnsureData();

        var found = new List<ChartItem>();

        if (Transform is not { } transform || _count == 0)
        {
            return found;
        }

        var plot = transform.PlotBounds;
        var range = RangeOf(BandDimension);

        if (!(plot.Width > 0) || !(plot.Height > 0) || range.IsEmpty)
        {
            return found;
        }

        var context = ContextOf(transform);

        if (!TryBandFacts(in context, out var facts))
        {
            return found;
        }

        var (start, length) = BandDimension == ChartDimension.X
            ? Band(range.Minimum, range.Maximum)
            : (0, _count);

        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);

        for (var index = start; index < start + length; index++)
        {
            if (Place(index, zeroX, zeroY, out var x, out var y) &&
                TryPoint(x, y, out var position) &&
                TryColumn(index, x, y, position, in context, in facts, out var column) &&
                column.Rect.Intersects(region))
            {
                found.Add(new ChartItem(this, index));
            }
        }

        return found;
    }

    /// <summary>Where a record is drawn: the end of its column the reading is at.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The position, or null when the record draws no column.</returns>
    /// <remarks>
    /// The end of the column rather than the value's place on the axis: a stack draws the reading above the ones
    /// below it, and a cluster draws it to the side of them, and a tip or a marker belongs where the ink is.
    /// </remarks>
    internal override Point? PositionOf(int index)
    {
        if (Transform is not { } transform || index < 0 || index >= _count)
        {
            return base.PositionOf(index);
        }

        var context = ContextOf(transform);
        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);

        return TryBandFacts(in context, out var facts) &&
            Place(index, zeroX, zeroY, out var x, out var y) &&
            TryPoint(x, y, out var place) &&
            TryColumn(index, x, y, place, in context, in facts, out var column)
                ? column.Value
                : base.PositionOf(index);
    }

    /// <summary>The context a pointer is answered in: the transform as it stands, and the bands it was drawn with.</summary>
    /// <param name="transform">The transform the series is registered with.</param>
    /// <returns>The context.</returns>
    private SeriesRenderContext ContextOf(DataTransform transform) =>
        new(transform, transform.PlotBounds, _scaling, _bands);

    /// <summary>How far a point is from a column, in pixels; zero inside it.</summary>
    /// <param name="column">The rectangle the record draws.</param>
    /// <param name="point">The position.</param>
    /// <param name="plot">The plot, which is where the ink stops.</param>
    /// <returns>The distance.</returns>
    /// <remarks>
    /// Only the part of a column inside the plot has ink - the layer clips what a series draws - so a column
    /// reaching past the edge is reached where it is drawn and not where it is not.
    /// </remarks>
    private static double DistanceTo(Rect column, Point point, Rect plot)
    {
        var drawn = column.Intersect(plot);

        if (!(drawn.Width > 0) && !(drawn.Height > 0))
        {
            return double.PositiveInfinity;
        }

        var across = Math.Max(0, Math.Max(drawn.Left - point.X, point.X - drawn.Right));
        var up = Math.Max(0, Math.Max(drawn.Top - point.Y, point.Y - drawn.Bottom));

        return Math.Sqrt((across * across) + (up * up));
    }

    /// <summary>How wide a column is, and how far it stands from its record's own place.</summary>
    /// <param name="Width">The width of one column, in pixels.</param>
    /// <param name="Shift">How far a column stands from the record's place, in pixels.</param>
    private readonly record struct BandFacts(double Width, double Shift);

    /// <summary>Works out how this series' columns stand in the band, or says they cannot be drawn.</summary>
    /// <param name="context">Where the plot is and which chart this is.</param>
    /// <param name="facts">What the columns are, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when there is no band for a column to stand in.</returns>
    private bool TryBandFacts(in SeriesRenderContext context, out BandFacts facts)
    {
        facts = default;

        if (AxisFor(BandDimension) is not { } axis)
        {
            return false;
        }

        var plot = context.PlotBounds;
        var length = BandDimension == ChartDimension.X ? plot.Width : plot.Height;
        var range = context.Transform.GetLayout(axis).UnitRange;

        if (range.IsEmpty || !(length > 0))
        {
            return false;
        }

        var (slot, slots) = context.Bands?.PlaceOf(this) ?? (0, 1);

        // The band is one unit of the axis, and this series gets its share of the fraction of it a column takes.
        var band = length / range.Length * ColumnWidthRatio;
        var width = band / slots;

        if (MaxColumnWidth is { } cap && width > cap)
        {
            width = cap;
        }

        if (!(width > 0))
        {
            return false;
        }

        // The cluster is centred on the slot, and this series stands in the middle of its own share of it.
        facts = new BandFacts(width, ((slot + 0.5) * width) - (band / 2));
        return true;
    }

    /// <summary>One record as it is drawn, or says the record draws nothing.</summary>
    /// <param name="index">The record's position in the data.</param>
    /// <param name="x">Its horizontal unit value, as drawn.</param>
    /// <param name="y">Its vertical unit value, as drawn.</param>
    /// <param name="position">Where the pair of values maps to.</param>
    /// <param name="context">Where the plot is.</param>
    /// <param name="facts">How the columns stand in the band.</param>
    /// <param name="column">The rectangle and the value end, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the record draws no column.</returns>
    /// <remarks>
    /// The one place a column's shape is worked out, so that what a pointer is answered with is what was drawn:
    /// the geometry pass and a hit test both come through here.
    /// </remarks>
    private bool TryColumn(
        int index,
        double x,
        double y,
        Point position,
        in SeriesRenderContext context,
        in BandFacts facts,
        out ColumnShape column)
    {
        column = default;

        // "Vertical" is the bars: the band runs up the plot and the values run across it.
        var vertical = BandDimension == ChartDimension.Y;
        var baseUnit = BaseUnit(index, in context);
        var own = vertical ? x : y;
        var zero = ZeroOn(ValueDimension);

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

        var centre = (vertical ? position.Y : position.X) + facts.Shift;

        // The band edges are layout and are lined up with the pixel grid so that columns tile; the value end
        // is data and stays where the value put it.
        var near = context.Snap(centre - (facts.Width / 2));
        var far = context.Snap(centre + (facts.Width / 2));

        if (!(far > near))
        {
            return false;
        }

        var value = vertical ? topPoint.X : topPoint.Y;
        var edge = vertical ? basePoint.X : basePoint.Y;
        var start = Math.Min(value, edge);
        var size = Math.Abs(value - edge);
        var middle = (near + far) / 2;

        column = new ColumnShape(
            vertical ? new Rect(start, near, size, far - near) : new Rect(near, start, far - near, size),
            vertical ? new Point(value, middle) : new Point(middle, value));

        return true;
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
