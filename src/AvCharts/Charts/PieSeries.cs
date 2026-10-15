using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Readings drawn as wedges of a circle: how much of the whole each of them is.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// A pie is not a chart of positions but of shares, so a reading is one number and the circle is the total of
/// them: the series adds up the values it is given and hands each record the slice of the circle it earned.
/// Nothing is asked of an axis, and a <see cref="ChartDimension"/> means nothing here - which is why this
/// series is drawn by a <see cref="PolarTransform"/> rather than by the Cartesian one.
/// </para>
/// <para>
/// The angles are Flex's: nought degrees is three o'clock and the wedges run anticlockwise, because that is
/// what its own pie does unless a host asks for the other direction - see <see cref="Clockwise"/> and
/// <see cref="StartAngle"/>. What a record is filled with comes from the chart's palette, one slot per record
/// in the order they were recorded, and the outline of a wedge is the pen of its own slot.
/// </para>
/// <para>
/// A record with no value - or with one that is not a number - draws no wedge and does not count towards the
/// total, which is the null-value rule the rest of the library follows: what is missing belongs in no
/// denominator. A negative reading is treated the same way rather than subtracting from the whole: a share of
/// a circle that is less than nothing is not a thing a reader can be shown.
/// </para>
/// </remarks>
public class PieSeries<T> : Series
{
    private readonly List<Geometry?> _wedges = [];
    private readonly List<double> _starts = [];
    private readonly List<double> _sweeps = [];
    private bool _sharesAreStale = true;
    private IReadOnlyList<Pen?>? _strokes;
    private IReadOnlyList<IBrush?>? _fills;
    private int _first;
    private double[] _values = [];
    private string?[] _names = [];
    private T[] _items = [];
    private int _count;
    private double _total;

    /// <summary>The records the series is drawn from.</summary>
    public IEnumerable<T>? ItemsSource
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            InvalidateData();
        }
    }

    /// <summary>How much of the whole a record is, which is what decides how wide its wedge is.</summary>
    public Func<T, ChartValue>? ValueSelector
    {
        get;
        set => Select(ref field, value);
    }

    /// <summary>What a record is called, for a data tip; unset, a wedge is named by where it stands.</summary>
    public Func<T, string?>? NameSelector
    {
        get;
        set => Select(ref field, value);
    }

    /// <summary>Which records are drawn; unset, all of them are.</summary>
    public Func<T, bool>? Filter
    {
        get;
        set => Select(ref field, value);
    }

    /// <summary>
    /// How wide the rim of the circle is, as a fraction of the circle's radius: nought by default, which is a
    /// whole pie; half is a ring with a hole half the circle across.
    /// </summary>
    public double InnerRadius
    {
        get;
        set
        {
            var wanted = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

            if (field.Equals(wanted))
            {
                return;
            }

            field = wanted;
            InvalidateGeometry();
        }
    }

    /// <summary>
    /// Where the first wedge starts, in degrees: nought is three o'clock, and the angles grow anticlockwise.
    /// </summary>
    public double StartAngle
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            _sharesAreStale = true;
            InvalidateGeometry();
        }
    }

    /// <summary>Whether the wedges run clockwise instead of anticlockwise; off by default, as Flex's pie is.</summary>
    public bool Clockwise
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _sharesAreStale = true;
            InvalidateGeometry();
        }
    }

    /// <summary>The number of records the series holds, whether or not they draw anything.</summary>
    public int Count
    {
        get
        {
            EnsureData();
            return _count;
        }
    }

    /// <summary>What the whole circle is worth: the total of the readings that draw a wedge.</summary>
    public double Total
    {
        get
        {
            EnsureData();
            return _total;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<ChartItem> Items =>
        Enumerable.Range(0, Count).Select(index => new ChartItem(this, index));

    /// <inheritdoc />
    /// <remarks>
    /// The colour a legend shows beside the name: the first slot of the palette, which is where this series'
    /// wedges start.
    /// </remarks>
    public override IBrush? Color => FillAt(0) ?? base.Color;

    /// <inheritdoc />
    public override void TakePalette(IReadOnlyList<Pen?> strokes, IReadOnlyList<IBrush?> fills, int first)
    {
        // Taken once and kept, and asking again with the same palette says nothing: the chart gives the palette
        // over every time a series of its own says it changed, and a series that answered each of those with
        // "I changed" would be a chart that never stopped talking to itself.
        if (ReferenceEquals(_strokes, strokes) && ReferenceEquals(_fills, fills) && _first == first)
        {
            return;
        }

        _strokes = strokes;
        _fills = fills;
        _first = first;

        // Colours are put on at draw time rather than built into the geometry, so this is a repaint and not a
        // chart's worth of work - the same rule the two financial colours follow.
        Repaint();
    }

    /// <summary>The share of the circle a record takes, as a fraction of the whole.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The share; nought when the record draws no wedge.</returns>
    public double ShareOf(int index)
    {
        EnsureShares();

        return index >= 0 && index < _sweeps.Count ? Math.Abs(_sweeps[index]) : 0;
    }

    /// <inheritdoc />
    protected override void UpdateData()
    {
        _count = 0;
        _total = 0;

        _wedges.Clear();

        _values = [];
        _names = [];
        _items = [];
        _sharesAreStale = true;

        var source = ItemsSource;

        if (source is null)
        {
            return;
        }

        var values = new List<double>();
        var names = new List<string?>();
        var items = new List<T>();
        var total = 0d;

        foreach (var item in source)
        {
            if (item is null || (Filter is { } filter && !filter(item)))
            {
                continue;
            }

            // A value that is missing, that is not a number, or that is less than nothing is no wedge at all
            // rather than a wedge that eats into the rest: see the remarks on the type.
            var value = ValueSelector is { } selector &&
                selector(item) is { IsEmpty: false } reading &&
                reading.TryGetNumber(out var number) &&
                !double.IsNaN(number) &&
                number > 0
                    ? number
                    : double.NaN;

            if (!double.IsNaN(value))
            {
                total += value;
            }

            values.Add(value);
            names.Add(NameSelector is { } name ? name(item) : null);
            items.Add(item);
        }

        _values = [.. values];
        _names = [.. names];
        _items = [.. items];
        _count = _values.Length;
        _total = total;
    }

    /// <inheritdoc />
    /// <remarks>A pie declares nothing: its wedges are shares of their own total rather than readings on a scale.</remarks>
    protected override void DescribeData(DataDescription description) => _ = description;

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        _wedges.Clear();
        EnsureShares();

        if (_count == 0 ||
            !context.TryUnitPoint(0, 1, out var onTheRim) ||
            !context.TryUnitPoint(0, 0, out var middle))
        {
            return;
        }

        // How far the circle reaches, worked out from the transform rather than assumed: the transform is what
        // knows where the circle is and how big, and a chart that gave the series another one - a circle drawn
        // smaller, a coordinate system of its own - is none of the series' business.
        var radius = Distance(middle, onTheRim);
        var inner = radius * InnerRadius;

        for (var index = 0; index < _count; index++)
        {
            var value = double.IsNaN(_values[index]) ? 0 : _values[index];

            _wedges.Add(
                _total > 0 && value > 0
                    ? Wedge(middle, radius, inner, _starts[index], _sweeps[index])
                    : null);
        }
    }

    /// <summary>Works out where every wedge starts and how far round it goes, if that is not up to date.</summary>
    /// <remarks>
    /// A share is a fact about the readings and the two knobs that decide how they are laid round the circle -
    /// not about the plot - so it is worked out when it is asked for rather than when something is drawn. A hit
    /// test happens between frames, with no geometry to look at, and it is this that it reads.
    /// </remarks>
    private void EnsureShares()
    {
        EnsureData();

        if (!_sharesAreStale)
        {
            return;
        }

        _sharesAreStale = false;
        _starts.Clear();
        _sweeps.Clear();

        var from = StartAngle / 360;
        var direction = Clockwise ? -1 : 1;

        for (var index = 0; index < _count; index++)
        {
            var worth = !double.IsNaN(_values[index]) && _values[index] > 0 && _total > 0
                ? _values[index] / _total
                : 0;
            var sweep = worth * direction;

            _starts.Add(from);
            _sweeps.Add(sweep);
            from += sweep;
        }
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        for (var index = 0; index < _wedges.Count; index++)
        {
            if (_wedges[index] is not { } wedge)
            {
                continue;
            }

            context.DrawGeometry(FillAt(index), StrokeAt(index), wedge);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The wedge the pointer is inside, which is a question about two angles and two radii rather than about a
    /// distance: the transform reads the pointer back as the angle and the radius that would have put it there.
    /// </remarks>
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        EnsureShares();

        if (_count == 0 || Transform is not { } transform ||
            !transform.TryPointToValue(point, out var angle, out var radius) ||
            !angle.TryGetNumber(out var turns) ||
            !radius.TryGetNumber(out var reach))
        {
            return null;
        }

        // A pointer just outside the rim, or just inside the hole, is still on the wedge it is beside: the
        // sensitivity the chart was asked for is how far off a record a pointer may be and still be about it.
        var circle = transform is PolarTransform polar ? polar.Radius : 0;
        var slack = circle > 0 ? sensitivity / circle : 0;

        if (reach < InnerRadius - slack || reach > 1 + slack)
        {
            return null;
        }

        for (var index = 0; index < _count; index++)
        {
            if (_sweeps[index] == 0)
            {
                continue;
            }

            if (Contains(index, turns))
            {
                return Hit(index, DistanceInRadians(index, turns) * circle);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public override IReadOnlyList<ChartItem> GetItemsInRegion(Rect region)
    {
        EnsureShares();

        var found = new List<ChartItem>();

        for (var index = 0; index < _count; index++)
        {
            if (_sweeps[index] != 0 && PositionOf(index) is { } position && region.Contains(position))
            {
                found.Add(new ChartItem(this, index));
            }
        }

        return found;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The middle of the wedge: half a sweep round from where it starts and half way out - which is where a
    /// data tip stands, and where the mark of a hovered or chosen wedge is drawn.
    /// </remarks>
    internal override Point? PositionOf(int index) =>
        index >= 0 && index < _starts.Count &&
        Transform is { } transform &&
        transform.TryUnitToPoint(_starts[index] + (_sweeps[index] / 2), (1 + InnerRadius) / 2, out var point)
            ? point
            : null;

    /// <inheritdoc />
    internal override object? SourceItemAt(int index) => index >= 0 && index < _count ? _items[index] : null;

    /// <summary>The fill of a record's wedge: the palette's slot for it, in the order the records came in.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The brush, or null when the chart has not been dressed.</returns>
    private IBrush? FillAt(int index) => _fills is { Count: > 0 } fills ? fills[Slot(index, fills.Count)] : PaletteFill;

    /// <summary>The pen around a record's wedge: the palette's pen for the same slot.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The pen, or null when the chart has not been dressed.</returns>
    private Pen? StrokeAt(int index) =>
        _strokes is { Count: > 0 } strokes ? strokes[Slot(index, strokes.Count)] : PaletteStroke;

    /// <summary>Which slot of the palette a record takes.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="size">How many slots the palette has.</param>
    /// <returns>The slot.</returns>
    /// <remarks>
    /// Counted from the slot the chart stood this series in, and cycling when there are more records than the
    /// palette holds - so a pie of twelve readings wears the ten colours twice rather than inventing two.
    /// </remarks>
    private int Slot(int index, int size) => (((_first + index) % size) + size) % size;

    /// <summary>Builds one wedge's geometry, in the chart's own coordinates.</summary>
    /// <param name="middle">The middle of the circle.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <param name="inner">How far the rim starts.</param>
    /// <param name="start">Where the wedge begins, in turns.</param>
    /// <param name="sweep">How far round it goes, in turns; negative when the series runs clockwise.</param>
    /// <returns>The geometry.</returns>
    private static Geometry Wedge(
        Point middle,
        double radius,
        double inner,
        double start,
        double sweep)
    {
        var batch = new GeometryBatch();

        if (Math.Abs(sweep) >= 1)
        {
            // All of the circle, which is what one record of a chart of one record is worth. An arc from a
            // point back to itself is no arc at all, so a whole turn is drawn as the two circles that make it
            // a ring - and the even-odd rule leaves the middle of those two empty, which is the hole.
            batch.AddCircle(middle, radius);

            if (inner > 0)
            {
                batch.AddCircle(middle, inner);
            }
        }
        else
        {
            batch.AddWedge(middle, radius, inner, start, start + sweep);
        }

        return batch.Build();
    }

    /// <summary>Whether an angle falls inside a record's wedge.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="turns">The angle, in turns between nought and one.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    /// <remarks>
    /// Both ends of the wedge are lengths rather than values - a value of a whole turn is the same angle as
    /// nought - so the comparison is made a distance round the circle from where the wedge starts.
    /// </remarks>
    private bool Contains(int index, double turns)
    {
        var sweep = _sweeps[index];

        if (sweep == 0)
        {
            return false;
        }

        // How far the pointer is round the circle from where the wedge starts, counted the way the wedge runs
        // and folded back into a whole turn: a pointer a hair before three o'clock is inside a wedge that begins
        // at nought and runs anticlockwise.
        var gap = sweep >= 0 ? turns - _starts[index] : _starts[index] - turns;

        return gap - Math.Floor(gap) < Math.Min(Math.Abs(sweep), 1);
    }

    /// <summary>How far a pointer is from a wedge, in radians, for the hit's own sense of distance.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="turns">The angle the pointer is at.</param>
    /// <returns>The distance.</returns>
    private double DistanceInRadians(int index, double turns)
    {
        var middle = _starts[index] + (_sweeps[index] / 2);
        var gap = Math.Abs(turns - middle);

        return Math.Min(gap, 1 - gap) * 2 * Math.PI;
    }

    /// <summary>The line a data tip shows for a record: what it is called, what it is worth, and its share.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The text.</returns>
    private string Describe(int index)
    {
        var name = _names[index] is { Length: > 0 } named ? named : $"Wedge {index + 1}";
        var value = double.IsNaN(_values[index]) ? "—" : _values[index].ToString("0.###", CultureInfo.InvariantCulture);
        var share = ShareOf(index) * 100;

        return $"{name}: {value} ({share.ToString("0.#", CultureInfo.InvariantCulture)}%)";
    }

    private HitData Hit(int index, double distance) => new()
    {
        Distance = distance,
        ChartItem = new ChartItem(this, index),
        DisplayText = Describe(index),
    };

    /// <summary>Sets one of the ways the source is read, reading it again only when it is a different one.</summary>
    /// <typeparam name="TValue">What the selector reads.</typeparam>
    /// <param name="field">The selector being set.</param>
    /// <param name="value">The selector to set.</param>
    private void Select<TValue>(ref TValue? field, TValue? value)
        where TValue : class
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        InvalidateData();
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(((first.X - second.X) * (first.X - second.X)) + ((first.Y - second.Y) * (first.Y - second.Y)));
}
