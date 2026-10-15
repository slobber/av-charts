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
    private readonly List<Placed> _labels = [];
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

    /// <summary>
    /// How far every wedge stands out from the middle, as a fraction of the circle's radius; nought by default.
    /// </summary>
    /// <remarks>
    /// What a reader calls a pushed-out pie, and what Flex calls <c>explodeRadius</c>: the whole ring of wedges
    /// moves out along its own middle, so the wedges stop touching and the circle opens up between them. A host
    /// that wants one wedge out rather than all of them asks for it per record, with <see cref="ExplodeSelector"/>.
    /// </remarks>
    public double ExplodeRadius
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
    /// How far one record's wedge stands out of its own, added to <see cref="ExplodeRadius"/>.
    /// </summary>
    /// <remarks>
    /// Flex's <c>perWedgeExplodeRadius</c>, as a selector because that is how this library reads a record. The
    /// two add up, so a series with the whole ring out and one wedge asked for a little more draws that wedge
    /// further out than the rest - which is what a reader pointing at the slice they care about is asking for.
    /// </remarks>
    public Func<T, double>? ExplodeSelector
    {
        get;
        set => Select(ref field, value);
    }

    /// <summary>What a wedge is written with; unset, no wedge is written on.</summary>
    /// <remarks>
    /// Any text, including a line break - Flex's <c>labelField</c> and <c>labelFunction</c> in one, because a
    /// reading that carries its own label is a reading a host has already worked the words out for.
    /// </remarks>
    public Func<T, string?>? LabelSelector
    {
        get;
        set => Select(ref field, value);
    }

    /// <summary>Where the labels are drawn; <see cref="PieLabelPosition.None"/> unless a host asks for more.</summary>
    public PieLabelPosition LabelPosition
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The brush the labels are written in; the chart hands over its own until a host sets one.</summary>
    public IBrush? LabelBrush
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The size of the labels, in points.</summary>
    public double LabelFontSize
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            InvalidateGeometry();
        }
    } = 11;

    /// <summary>The face the labels are set in.</summary>
    public Typeface LabelTypeface
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            InvalidateGeometry();
        }
    } = new(FontFamily.Default);

    /// <inheritdoc />
    public override void TakeLabelInk(IBrush ink)
    {
        if (ReferenceEquals(LabelBrush, ink))
        {
            return;
        }

        LabelBrush = ink;
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

        if (_count == 0 || !TryCircle(out var middle, out var radius))
        {
            _labels.Clear();
            return;
        }

        var inner = radius * InnerRadius;

        for (var index = 0; index < _count; index++)
        {
            var value = double.IsNaN(_values[index]) ? 0 : _values[index];

            _wedges.Add(
                _total > 0 && value > 0
                    ? Wedge(CentreOf(index, middle, radius), radius, inner, _starts[index], _sweeps[index])
                    : null);
        }

        PlaceLabels(in context, middle, radius);
    }

    /// <summary>Works out where every label goes, which is the half of a labelled pie that has to be thought about.</summary>
    /// <param name="context">Where the plot is, which is how far a label may be pushed and still be on the picture.</param>
    /// <param name="middle">The middle of the circle.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <remarks>
    /// <para>
    /// A label inside a wedge is drawn when the wedge has room for it - the width of the wedge at the radius the
    /// text stands at, and its depth, each with a little room to spare - and left out when it has not. Flex
    /// shrinks such a label instead and drops it once it would fall below nine points; a pie whose labels are
    /// each a different size reads as a mistake, so this drops rather than shrinks.
    /// </para>
    /// <para>
    /// Labels outside the circle are put beside the wedge that earned them and then told to stop standing on each
    /// other: within each side of the circle they are pushed apart from the top down, the whole stack is lifted
    /// if it has grown past the bottom of the plot, and any label that still does not fit is left out - the same
    /// rule the axis labels follow.
    /// </para>
    /// </remarks>
    private void PlaceLabels(in SeriesRenderContext context, Point middle, double radius)
    {
        _labels.Clear();

        if (LabelSelector is null || LabelPosition == PieLabelPosition.None || LabelBrush is null)
        {
            return;
        }

        var wanted = new List<Wanted>();

        for (var index = 0; index < _count; index++)
        {
            if (_sweeps[index] == 0 ||
                LabelFor(index) is not { Width: > 0 } text ||
                !TryMiddleOf(index, middle, radius, out var at))
            {
                continue;
            }

            wanted.Add(
                Frame(
                    index,
                    text,
                    CentreOf(index, middle, radius),
                    radius,
                    at,
                    LabelPosition == PieLabelPosition.Outside));
        }

        if (LabelPosition == PieLabelPosition.Inside)
        {
            foreach (var label in wanted.Where(
                one => FitsInside(one.Index, one.Text, one.Centre, radius, one.Middle)))
            {
                _labels.Add(
                    new Placed(
                        label.Text,
                        new Point(label.Middle.X - (label.Text.Width / 2), label.Middle.Y - (label.Text.Height / 2)),
                        []));
            }

            return;
        }

        // Outside: the labels are put beside the wedge that earned them and then told to stop standing on each
        // other - within each side of the circle, because a label on the left has nothing to do with one on the
        // right and pushing them about together would only move the whole picture.
        foreach (var side in wanted.GroupBy(one => one.OnTheRight))
        {
            var stack = side.OrderBy(one => one.Y).ToList();

            Lift(stack, context.PlotBounds);

            foreach (var one in stack)
            {
                var origin = new Point(one.X, one.Y - (one.Text.Height / 2));
                var box = new Rect(origin, new Size(one.Text.Width, one.Text.Height));

                // A label that cannot be got onto the picture is left off it, which is the same rule the axis
                // labels follow: a word half off the edge of a chart is worse than a word that is not there.
                if (!context.PlotBounds.Contains(box))
                {
                    continue;
                }

                var near = one.OnTheRight ? origin.X - 2 : origin.X + one.Text.Width + 2;

                _labels.Add(new Placed(one.Text, origin, [one.Rim, one.Elbow, new Point(near, one.Y)]));
            }
        }
    }

    /// <summary>Frames one label: where it wants to be, where its leader starts, and which side it is on.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="text">The laid-out text.</param>
    /// <param name="centre">The centre of the wedge.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <param name="at">The middle of the wedge, which is where an inside label stands.</param>
    /// <param name="outside">Whether the label is to stand beside the circle rather than in the wedge.</param>
    /// <returns>The label as it wants to be drawn.</returns>
    private Wanted Frame(int index, FormattedText text, Point centre, double radius, Point at, bool outside)
    {
        if (!outside)
        {
            return new Wanted(index, text, at, centre, at.X, at.Y, at, at, false);
        }

        var radians = (_starts[index] + (_sweeps[index] / 2)) * 2 * Math.PI;
        var across = Math.Cos(radians);
        var up = -Math.Sin(radians);
        var rim = new Point(centre.X + (across * radius), centre.Y + (up * radius));
        var elbow = new Point(rim.X + (across * LabelGap), rim.Y + (up * LabelGap));
        var right = across >= 0;

        return new Wanted(
            index,
            text,
            at,
            centre,
            right ? elbow.X + (LabelGap / 2) : elbow.X - (LabelGap / 2) - text.Width,
            elbow.Y,
            rim,
            elbow,
            right);
    }

    /// <summary>Pushes a stack of labels apart, and lifts the whole stack when it has grown past the bottom.</summary>
    /// <param name="stack">The labels on one side of the circle, ordered by where they want to be.</param>
    /// <param name="plot">The rectangle the stack has to live in.</param>
    private static void Lift(List<Wanted> stack, Rect plot)
    {
        // Two passes: one that pushes each label below the one before it, and one that lifts the whole stack if
        // it has grown past the bottom of the plot and then pushes them apart again.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var index = 1; index < stack.Count; index++)
            {
                var above = stack[index - 1];
                var below = stack[index];
                var wanted = above.Y + (above.Text.Height / 2) + Between + (below.Text.Height / 2);

                if (below.Y < wanted)
                {
                    stack[index] = below with { Y = wanted };
                }
            }

            if (stack.Count == 0)
            {
                return;
            }

            var last = stack[^1];
            var overflow = last.Y + (last.Text.Height / 2) - plot.Bottom;

            if (!(overflow > 0))
            {
                return;
            }

            for (var index = 0; index < stack.Count; index++)
            {
                stack[index] = stack[index] with { Y = stack[index].Y - overflow };
            }
        }
    }

    /// <summary>Whether a wedge has room for its label: whether the whole of the text is inside it.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="text">The laid-out text.</param>
    /// <param name="centre">The centre of the wedge.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <param name="at">The middle of the wedge, which is where the text would stand.</param>
    /// <returns><see langword="true"/> when the text fits inside the wedge.</returns>
    /// <remarks>
    /// The four corners of the text's box are asked whether they are in the wedge, which is the same question the
    /// pointer is asked and the only one that behaves at both ends of the range: a two-degree slice has room for
    /// nothing at its rim, and a slice of nine tenths of the circle has room for nearly the whole of it. Measuring
    /// the mouth of the wedge instead - which is what this did first - gets the second of those wrong, because the
    /// chord of an arc almost all the way round is the chord of the sliver it leaves out.
    /// </remarks>
    private bool FitsInside(int index, FormattedText text, Point centre, double radius, Point at)
    {
        var left = at.X - (text.Width / 2);
        var top = at.Y - (text.Height / 2);
        var right = left + text.Width;
        var bottom = top + text.Height;

        return Holds(index, new Point(left, top), centre, radius) &&
            Holds(index, new Point(right, top), centre, radius) &&
            Holds(index, new Point(left, bottom), centre, radius) &&
            Holds(index, new Point(right, bottom), centre, radius);
    }

    /// <summary>Whether a place is inside a record's wedge.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="point">The place.</param>
    /// <param name="centre">The centre of the wedge.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool Holds(int index, Point point, Point centre, double radius)
    {
        var across = point.X - centre.X;
        var up = centre.Y - point.Y;
        var reach = Math.Sqrt((across * across) + (up * up)) / radius;

        if (reach < InnerRadius || reach > 1)
        {
            return false;
        }

        var turns = Math.Atan2(up, across) / (2 * Math.PI);

        turns -= Math.Floor(turns);
        return Contains(index, turns);
    }

    /// <summary>One record's label, laid out and ready to be placed; null when the record has none.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The text, or null.</returns>
    private FormattedText? LabelFor(int index)
    {
        if (LabelSelector is not { } selector ||
            LabelBrush is not { } ink ||
            index < 0 ||
            index >= _count ||
            _items[index] is not T item ||
            selector(item) is not { Length: > 0 } words)
        {
            return null;
        }

        return new FormattedText(
            words,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            LabelFontSize,
            ink);
    }

    /// <summary>Where a record's wedge is drawn from: the middle of the circle, moved out by how far it stands out.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="middle">The middle of the circle.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <returns>The centre the wedge's arcs are drawn about.</returns>
    /// <remarks>
    /// A wedge stands out along its own middle, which is what makes a pushed-out pie look pushed out rather than
    /// shifted: every wedge leaves the middle of the circle in the direction it is pointing. Flex places the
    /// wedge's origin the same way, and the wedge keeps its shape - it is the same arc of the same circle, drawn
    /// about a centre that moved.
    /// </remarks>
    private Point CentreOf(int index, Point middle, double radius)
    {
        var out3 = ExplodeRadius + ExplodeAt(index);

        if (!(out3 > 0))
        {
            return middle;
        }

        var radians = (_starts[index] + (_sweeps[index] / 2)) * 2 * Math.PI;
        var distance = Math.Min(out3, 1) * radius;

        return new Point(
            middle.X + (Math.Cos(radians) * distance),
            middle.Y - (Math.Sin(radians) * distance));
    }

    /// <summary>How far one record asked to stand out of its own, or nought.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The distance, as a fraction of the radius.</returns>
    private double ExplodeAt(int index)
    {
        if (ExplodeSelector is not { } selector || index < 0 || index >= _count || _items[index] is not T item)
        {
            return 0;
        }

        var wanted = selector(item);

        return double.IsNaN(wanted) ? 0 : Math.Clamp(wanted, 0, 1);
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

        if (_labels.Count == 0 || LabelBrush is not { } ink)
        {
            return;
        }

        // The line that reaches a label is a hairline in the same ink as the text: it is part of the label rather
        // than part of the chart, so it is drawn in what the words are drawn in.
        var pen = new Pen(ink, 1);

        foreach (var label in _labels)
        {
            for (var point = 1; point < label.Leader.Count; point++)
            {
                context.DrawLine(pen, label.Leader[point - 1], label.Leader[point]);
            }

            context.DrawText(label.Text, label.Origin);
        }
    }

    /// <summary>How far a label stands from the circle, and how far apart two labels on one side stand.</summary>
    /// <remarks>Flex's <c>calloutGap</c>, which is ten and is what a leader line needs to be seen at all.</remarks>
    private const double LabelGap = 10;

    /// <summary>How much room is left between two labels that have been pushed apart.</summary>
    private const double Between = 2;

    /// <summary>One label as it wants to be drawn, before it is told where it may be drawn.</summary>
    /// <param name="Index">The record it belongs to.</param>
    /// <param name="Text">The laid-out text.</param>
    /// <param name="Middle">The middle of the wedge, which is where an inside label stands.</param>
    /// <param name="Centre">The centre of the wedge, which is what the room for the label is measured from.</param>
    /// <param name="X">Where the middle of the text wants to be across.</param>
    /// <param name="Y">Where it wants to be up and down.</param>
    /// <param name="Rim">Where the leader starts, on the wedge's own rim.</param>
    /// <param name="Elbow">Where the leader turns, just outside the rim.</param>
    /// <param name="OnTheRight">Which side of the circle it is on.</param>
    private sealed record Wanted(
        int Index,
        FormattedText Text,
        Point Middle,
        Point Centre,
        double X,
        double Y,
        Point Rim,
        Point Elbow,
        bool OnTheRight);

    /// <summary>One label where it is actually drawn.</summary>
    /// <param name="Text">The laid-out text.</param>
    /// <param name="Origin">Where the text is drawn, at its top left.</param>
    /// <param name="Leader">The points of the line that reaches it, or nothing for a label inside a wedge.</param>
    private sealed record Placed(FormattedText Text, Point Origin, IReadOnlyList<Point> Leader);

    /// <inheritdoc />
    /// <remarks>
    /// The wedge the pointer is inside, which is a question about two angles and two radii rather than about a
    /// distance: the transform reads the pointer back as the angle and the radius that would have put it there.
    /// </remarks>
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        EnsureShares();

        if (_count == 0 || !TryCircle(out var middle, out var radius))
        {
            return null;
        }

        // A pointer just outside the rim, or just inside the hole, is still on the wedge it is beside: the
        // sensitivity the chart was asked for is how far off a record a pointer may be and still be about it.
        var slack = sensitivity / radius;

        for (var index = 0; index < _count; index++)
        {
            if (_sweeps[index] == 0)
            {
                continue;
            }

            // Every wedge is measured from its own centre, because that is where it was drawn from: a wedge that
            // stands out of the ring has moved, and the pointer that is on it has moved with it.
            var centre = CentreOf(index, middle, radius);
            var across = point.X - centre.X;
            var up = centre.Y - point.Y;
            var reach = Math.Sqrt((across * across) + (up * up)) / radius;

            if (reach < InnerRadius - slack || reach > 1 + slack)
            {
                continue;
            }

            var turns = Math.Atan2(up, across) / (2 * Math.PI);

            turns -= Math.Floor(turns);

            if (Contains(index, turns))
            {
                return Hit(index, DistanceInRadians(index, turns) * radius);
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
        TryCircle(out var middle, out var radius) &&
        TryMiddleOf(index, middle, radius, out var point)
            ? point
            : null;

    /// <summary>Where a record's wedge has its middle, which is where its label, tip and mark stand.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="middle">The middle of the circle.</param>
    /// <param name="radius">How far the circle reaches.</param>
    /// <param name="point">The middle of the wedge, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the record has no middle to stand in.</returns>
    private bool TryMiddleOf(int index, Point middle, double radius, out Point point)
    {
        point = default;

        if (index < 0 || index >= _starts.Count || _sweeps[index] == 0)
        {
            return false;
        }

        var centre = CentreOf(index, middle, radius);
        var radians = (_starts[index] + (_sweeps[index] / 2)) * 2 * Math.PI;
        var distance = (1 + InnerRadius) / 2 * radius;

        point = new Point(
            centre.X + (Math.Cos(radians) * distance),
            centre.Y - (Math.Sin(radians) * distance));

        return true;
    }

    /// <summary>
    /// The circle the wedges are drawn in: where it is and how far it reaches, read off the transform.
    /// </summary>
    /// <param name="middle">The middle of the circle.</param>
    /// <param name="radius">How far it reaches, in pixels.</param>
    /// <returns><see langword="false"/> when there is no circle to draw in.</returns>
    /// <remarks>
    /// Worked out from the transform rather than assumed: the transform is what knows where the circle is and
    /// how big, and a chart that gave the series another one - a circle drawn smaller, a coordinate system of
    /// its own - is none of the series' business.
    /// </remarks>
    private bool TryCircle(out Point middle, out double radius)
    {
        middle = default;
        radius = 0;

        if (Transform is not { } transform ||
            !transform.TryUnitToPoint(0, 1, out var onTheRim) ||
            !transform.TryUnitToPoint(0, 0, out middle))
        {
            return false;
        }

        radius = Distance(middle, onTheRim);
        return radius > 0;
    }

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
