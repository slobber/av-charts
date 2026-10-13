using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;

namespace AvCharts.Charts;

/// <summary>
/// A series whose two dimensions are the horizontal and vertical axes of a Cartesian chart.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is read, filter, select, normalize. A record is read from the source, dropped whole if the
/// filter says so, turned into a pair of <see cref="ChartValue"/>s by the selectors, and measured in unit space
/// - the axis' own arithmetic. Unit space is where the values live from then on, as two arrays of
/// <see cref="double"/>: sixteen bytes a point, no boxes, and nothing downstream ever touches the caller's
/// objects again.
/// </para>
/// <para>
/// A value that cannot be placed - missing, of a kind the axis does not understand, or on a dimension with no
/// axis - is stored as <see cref="double.NaN"/> and breaks the run rather than being moved to the origin. That
/// is the null policy the design brief settled on, and the reason a dropped sample in a sensor feed shows up as
/// a break instead of a dive to zero. A record the filter drops is a different matter: it takes up no room at
/// all, and the records after it close up behind it. The two are deliberately not the same thing.
/// </para>
/// <para>
/// A source that announces its changes is followed: a point appended to a live feed costs the new point, and a
/// record replaced where it stands costs that record, rather than a pass over the series. Anything else - a
/// record inserted or removed in the middle - moves every position after it, so the series reads its source
/// again rather than pretend the change was small.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="ItemsSource"/>.</typeparam>
public abstract class CartesianSeries<T> : Series
{
    // private protected: the values themselves are what a subclass in this assembly draws from, while the spans
    // below are what a subclass anywhere gets to look at.
    private protected double[] _x = [];
    private protected double[] _y = [];
    private protected T[] _items = [];
    private protected int _count;
    private bool _ascending = true;

    private IEnumerable<T>? _itemsSource;
    private INotifyCollectionChanged? _announcing;
    private readonly HashSet<INotifyPropertyChanged> _watched = new(ReferenceEqualityComparer.Instance);

    private IAxis? _horizontalAxis;
    private IAxis? _verticalAxis;

    private Func<T, ChartValue>? _xSelector;
    private Func<T, ChartValue>? _ySelector;
    private Func<T, bool>? _filter;
    private SamplingMode _sampling = SamplingMode.None;

    private readonly object _pushGate = new();
    private readonly List<T> _waiting = [];
    private bool _applying;

    /// <summary>
    /// The items to show. A source that announces its changes - an <see cref="INotifyCollectionChanged"/>, which
    /// is what an observable collection is - is followed as it changes; one that does not is read again when
    /// something asks the series for its values, so a plain list that changed underneath wants
    /// <see cref="Series.InvalidateData"/>.
    /// </summary>
    public IEnumerable<T>? ItemsSource
    {
        get => _itemsSource;
        set
        {
            if (ReferenceEquals(_itemsSource, value))
            {
                return;
            }

            StopListening();
            _itemsSource = value;
            StartListening(value);
            InvalidateData();
        }
    }

    /// <summary>How many records have been pushed and not applied yet. Safe to read from any thread.</summary>
    public int PendingPushes
    {
        get
        {
            lock (_pushGate)
            {
                return _waiting.Count;
            }
        }
    }

    /// <summary>
    /// Hands the series one record from wherever it came from - a device thread, a feed callback, a worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing is drawn, and nothing is measured, until <see cref="ApplyPushed"/> is called; the chart does that
    /// once a frame. That is the arrangement the design brief asks for: a feed of a thousand points a second
    /// costs one repaint a frame rather than a thousand, and the series' values are only ever written on the
    /// thread the series is drawn on.
    /// </para>
    /// <para>
    /// A pushed record is added to <see cref="ItemsSource"/> when the batch is applied, so the source holds
    /// everything the series shows and a later read of it agrees. That is also why a source has to be one that
    /// can be added to, and why this says so at the point of the mistake rather than losing the record quietly.
    /// </para>
    /// </remarks>
    /// <param name="item">The record.</param>
    public void Push(T item)
    {
        // An array is an ICollection<T> and says so, which is why the question asked is whether the collection
        // will take an item rather than whether it has the shape of one that could.
        if (_itemsSource is not ICollection<T> { IsReadOnly: false })
        {
            throw new InvalidOperationException(
                "A series can only be pushed to when ItemsSource is a collection that can be added to, because a "
                + "pushed record is added to the source when its batch is applied.");
        }

        lock (_pushGate)
        {
            _waiting.Add(item);
        }

        OnPushed();
    }

    /// <summary>
    /// How to read the horizontal value of an item. Left unset, the value is the position of the record among the
    /// records the series keeps - which is what a line of plain values wants, and what makes a record the filter
    /// dropped leave no trace at all.
    /// </summary>
    public Func<T, ChartValue>? XSelector
    {
        get => _xSelector;
        set
        {
            if (ReferenceEquals(_xSelector, value))
            {
                return;
            }

            _xSelector = value;
            InvalidateData();
        }
    }

    /// <summary>How to read the vertical value of an item. Left unset, every point is missing.</summary>
    public Func<T, ChartValue>? YSelector
    {
        get => _ySelector;
        set
        {
            if (ReferenceEquals(_ySelector, value))
            {
                return;
            }

            _ySelector = value;
            InvalidateData();
        }
    }

    /// <summary>
    /// Which records to drop. Dropping a record removes it from the series altogether - it takes up no slot and
    /// counts for nothing - whereas a missing value keeps its place and is only drawn as a gap.
    /// </summary>
    public Func<T, bool>? Filter
    {
        get => _filter;
        set
        {
            if (ReferenceEquals(_filter, value))
            {
                return;
            }

            _filter = value;
            InvalidateData();
        }
    }

    /// <summary>
    /// How to thin the series out when it has more points than the plot has pixel columns to draw them in.
    /// </summary>
    /// <remarks>
    /// Sampling switches itself off when it has nothing to do: a run that already fits in the columns comes back
    /// untouched, so turning this on never costs a series whose points are all visible - which is the rule the
    /// design brief asks for, kept in one place rather than at every call site.
    /// </remarks>
    public SamplingMode Sampling
    {
        get => _sampling;
        set
        {
            if (_sampling == value)
            {
                return;
            }

            _sampling = value;
            InvalidateGeometry();
        }
    }

    /// <summary>
    /// The horizontal values, in unit space, as <see cref="double.NaN"/> where a record has none: the only thing
    /// the drawing code reads, and the reason a selector runs once per change rather than once per frame.
    /// </summary>
    /// <remarks>
    /// A view onto the series' own storage, not a copy; it is good until the series reads its source again.
    /// </remarks>
    protected ReadOnlySpan<double> XValues
    {
        get
        {
            EnsureData();
            return _x.AsSpan(0, _count);
        }
    }

    /// <summary>The vertical values, in unit space, as <see cref="double.NaN"/> where a record has none.</summary>
    protected ReadOnlySpan<double> YValues
    {
        get
        {
            EnsureData();
            return _y.AsSpan(0, _count);
        }
    }

    /// <summary>
    /// Maps the values onto positions and groups them into runs. A value that has no position ends the run it
    /// was in and starts a new one after it, so a gap in the data is a gap in the ink.
    /// </summary>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    /// <returns>The runs, in data order; runs of one point are kept, because a lone marker is still a point.</returns>
    protected IReadOnlyList<IReadOnlyList<Point>> BuildRuns(in SeriesRenderContext context)
    {
        var runs = new List<IReadOnlyList<Point>>();
        var x = XValues;
        var y = YValues;
        List<Point>? current = null;

        // Where the axes put zero, for a series that draws missing values there. An axis with no zero - a
        // logarithmic one, or a category one - has nothing to offer, and the value stays missing.
        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);

        for (var index = 0; index < x.Length; index++)
        {
            if (Place(index, zeroX, zeroY, out var horizontal, out var vertical) &&
                TryPoint(horizontal, vertical, out var point))
            {
                current ??= [];
                current.Add(point);
                continue;
            }

            // Nothing was placed. Connecting keeps the run alive across the gap; the other two modes end it
            // here, which for a gap is exactly what the reader is supposed to see.
            if (NullValueMode == NullValueMode.Connect)
            {
                continue;
            }

            if (current is { Count: > 0 })
            {
                runs.Add(current);
            }

            current = null;
        }

        if (current is { Count: > 0 })
        {
            runs.Add(current);
        }

        if (_sampling == SamplingMode.None)
        {
            return runs;
        }

        // One bucket per pixel column, and only within a run: a gap in the data was already split into separate
        // runs above, so thinning can never draw a line across it.
        var columns = (int)Math.Round(context.PlotBounds.Width);
        var sampled = new List<IReadOnlyList<Point>>(runs.Count);

        foreach (var run in runs)
        {
            sampled.Add(Sampler.Reduce(run, columns, _sampling));
        }

        return sampled;
    }

    /// <summary>The unit value the axis of a dimension puts a data value of zero at, or NaN if it has no zero.</summary>
    private double ZeroUnit(ChartDimension dimension) =>
        AxisFor(dimension) is { } axis && axis.TryToUnit(ChartValue.FromNumber(0), out var unit)
            ? unit
            : double.NaN;

    /// <summary>
    /// Where the axis of a dimension puts a data value of zero, in unit space; NaN when it has no zero to put it
    /// at, as a logarithmic or a categorical axis has not.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The unit value, or NaN.</returns>
    protected double ZeroOn(ChartDimension dimension) => ZeroUnit(dimension);

    /// <summary>
    /// Places a pair of unit values on the plot, measured against this series' own axes.
    /// </summary>
    /// <param name="x">The horizontal value, in unit space.</param>
    /// <param name="y">The vertical value, in unit space.</param>
    /// <param name="point">The position, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the pair has no place on the chart.</returns>
    /// <remarks>
    /// The series maps its own values rather than asking the transform to, because the transform maps the chart's
    /// own axes and a series is entitled to an axis of its own. Everything else about it - that a missing value
    /// has no place, that the plot has to have room - is the same either way.
    /// </remarks>
    protected bool TryPoint(double x, double y, out Point point)
    {
        point = default;

        if (Transform is not { } transform ||
            AxisFor(ChartDimension.X) is not { } horizontal ||
            AxisFor(ChartDimension.Y) is not { } vertical)
        {
            return false;
        }

        var plot = transform.PlotBounds;
        var horizontalRange = transform.GetLayout(horizontal).UnitRange;
        var verticalRange = transform.GetLayout(vertical).UnitRange;

        // A range with no width is not a failure: it maps to the middle of the plot, as AxisUnitRange says it does,
        // which is what a series of one value or of a hundred identical ones draws as - a line down the centre.
        if (!(plot.Width > 0) || !(plot.Height > 0) || double.IsNaN(x) || double.IsNaN(y))
        {
            return false;
        }

        point = new Point(
            plot.X + (horizontalRange.Normalize(x) * plot.Width),
            plot.Bottom - (verticalRange.Normalize(y) * plot.Height));

        return true;
    }

    /// <summary>The stretch of unit space this series' axis for a dimension covers.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <returns>The range, or an empty one when the series has no axis there.</returns>
    protected AxisUnitRange RangeOf(ChartDimension dimension) =>
        Transform is { } transform && AxisFor(dimension) is { } axis
            ? transform.GetLayout(axis).UnitRange
            : AxisUnitRange.Empty;

    /// <summary>
    /// The records that have a place on the chart, with their values in unit space and where each of them goes.
    /// </summary>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    /// <returns>
    /// One entry per record that is drawn, in data order: its position in the series, its horizontal and vertical
    /// values in unit space, and the point it is drawn at. A record that is not drawn - because it has no value
    /// and the series is not drawing missing values at zero - is not here at all.
    /// </returns>
    /// <remarks>
    /// Where the null policy meets the mapping, so that a line's runs, a column's rectangles and a hit test all
    /// agree about which records exist and where they are. A subclass drawing something other than a line wants
    /// this rather than <see cref="BuildRuns"/>, which is about runs.
    /// </remarks>
    protected IEnumerable<(int Index, double X, double Y, Point Position)> PlacedRecords(
        SeriesRenderContext context)
    {
        // The arrays are read directly rather than through XValues, because a span cannot live across a yield.
        EnsureData();

        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);

        for (var index = 0; index < _count; index++)
        {
            if (Place(index, zeroX, zeroY, out var horizontal, out var vertical) &&
                TryPoint(horizontal, vertical, out var point))
            {
                yield return (index, horizontal, vertical, point);
            }
        }
    }

    /// <summary>
    /// Where a missing value on a dimension is drawn: the axis' zero when the series asks for that, and nowhere
    /// at all otherwise.
    /// </summary>
    private double ZeroFor(ChartDimension dimension) =>
        NullValueMode == NullValueMode.Zero ? ZeroUnit(dimension) : double.NaN;

    /// <summary>The unit pair a record is drawn at, applying the null policy; false when it is not drawn.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="zeroX">Where the horizontal axis puts zero, or NaN when it has no zero.</param>
    /// <param name="zeroY">Where the vertical axis puts zero, or NaN when it has no zero.</param>
    /// <param name="x">The horizontal unit value the record is drawn at.</param>
    /// <param name="y">The vertical unit value the record is drawn at.</param>
    /// <returns><see langword="false"/> when the record has no place on the chart.</returns>
    /// <remarks>
    /// The one place the null policy is applied, so that what is drawn, what a hit test finds and what a region
    /// query returns are all the same set of records - there is no second opinion about it to drift.
    /// </remarks>
    private bool Place(int index, double zeroX, double zeroY, out double x, out double y)
    {
        x = double.NaN;
        y = double.NaN;

        if (index < 0 || index >= _count)
        {
            return false;
        }

        x = double.IsNaN(_x[index]) && !double.IsNaN(zeroX) ? zeroX : _x[index];
        y = double.IsNaN(_y[index]) && !double.IsNaN(zeroY) ? zeroY : _y[index];

        return !double.IsNaN(x) && !double.IsNaN(y);
    }

    /// <inheritdoc />
    public override IEnumerable<ChartItem> Items
    {
        get
        {
            // "In view" is the horizontal axis' own range, which is the window a scrolling chart shows. A record
            // with no horizontal value has no place in the window; a record with a missing vertical value is
            // still a record, and appears with an empty Y - which is how "no reading" stays different from zero.
            var range = RangeOf(ChartDimension.X);

            EnsureData();

            if (range.IsEmpty)
            {
                yield break;
            }

            var zeroX = ZeroFor(ChartDimension.X);

            for (var index = 0; index < _count; index++)
            {
                var x = double.IsNaN(_x[index]) && !double.IsNaN(zeroX) ? zeroX : _x[index];

                if (!double.IsNaN(x) && x >= range.Minimum && x <= range.Maximum)
                {
                    yield return new ChartItem(this, index);
                }
            }
        }
    }

    /// <inheritdoc />
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        EnsureData();

        if (Transform is not { } transform || !(sensitivity > 0) || _count == 0)
        {
            return null;
        }

        var plot = transform.PlotBounds;
        var range = RangeOf(ChartDimension.X);

        if (!(plot.Width > 0) || range.IsEmpty)
        {
            return null;
        }

        // The index turns the pointer's reach into a band of records to look at rather than a walk of every one
        // of them, which is what keeps a hit test on a hundred thousand points inside its budget.
        var from = range.Denormalize((point.X - sensitivity - plot.X) / plot.Width);
        var to = range.Denormalize((point.X + sensitivity - plot.X) / plot.Width);
        var (start, length) = Band(Math.Min(from, to), Math.Max(from, to));
        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);
        HitData? best = null;

        for (var index = start; index < start + length; index++)
        {
            if (!Place(index, zeroX, zeroY, out var x, out var y) ||
                !TryPoint(x, y, out var position))
            {
                continue;
            }

            var distance = Distance(position, point);

            if (distance <= sensitivity && (best is null || distance < best.Value.Distance))
            {
                best = Hit(index, distance);
            }
        }

        return best;
    }

    /// <inheritdoc />
    public override IReadOnlyList<ChartItem> GetItemsInRegion(Rect region)
    {
        EnsureData();

        var found = new List<ChartItem>();

        if (Transform is not { } transform || _count == 0)
        {
            return found;
        }

        var plot = transform.PlotBounds;
        var range = RangeOf(ChartDimension.X);

        if (!(plot.Width > 0) || !(plot.Height > 0) || range.IsEmpty)
        {
            return found;
        }

        var from = range.Denormalize((region.Left - plot.X) / plot.Width);
        var to = range.Denormalize((region.Right - plot.X) / plot.Width);
        var (start, length) = Band(Math.Min(from, to), Math.Max(from, to));
        var zeroX = ZeroFor(ChartDimension.X);
        var zeroY = ZeroFor(ChartDimension.Y);

        for (var index = start; index < start + length; index++)
        {
            if (Place(index, zeroX, zeroY, out var x, out var y) &&
                TryPoint(x, y, out var position) &&
                region.Contains(position))
            {
                found.Add(new ChartItem(this, index));
            }
        }

        return found;
    }

    /// <inheritdoc />
    internal override double UnitAt(ChartDimension dimension, int index)
    {
        if (index < 0 || index >= _count)
        {
            return double.NaN;
        }

        if (dimension == ChartDimension.X)
        {
            return _x[index];
        }

        return dimension == ChartDimension.Y ? _y[index] : double.NaN;
    }

    /// <inheritdoc />
    internal override object? SourceItemAt(int index) => index >= 0 && index < _count ? _items[index] : null;

    /// <inheritdoc />
    internal override Point? PositionOf(int index) =>
        Place(index, ZeroFor(ChartDimension.X), ZeroFor(ChartDimension.Y), out var x, out var y) &&
        TryPoint(x, y, out var point)
            ? point
            : null;

    /// <summary>
    /// A stretch of the values whose horizontal unit lies inside a band, as a start and a length.
    /// </summary>
    /// <remarks>
    /// The values are the index: a series of readings arrives with its horizontal values in order, which makes the
    /// array of them something to search rather than something to walk. A series that arrives out of order has no
    /// index and the query walks it instead - correct either way, and the cheap way wherever readings are.
    /// </remarks>
    private (int Start, int Length) Band(double from, double to)
    {
        if (!_ascending || _count == 0)
        {
            return (0, _count);
        }

        var start = LowerBound(from);
        var end = LowerBound(to);

        // A record exactly on the far edge of the band is in it, so the band reaches as far as it says it does.
        while (end < _count && _x[end] <= to)
        {
            end++;
        }

        return (start, Math.Max(0, end - start));
    }

    /// <summary>The first record whose horizontal value is not below a unit value.</summary>
    private int LowerBound(double unit)
    {
        var low = 0;
        var high = _count;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (_x[middle] < unit)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>The line a data tip shows for a record: its values, written the way the axes write them.</summary>
    private string Describe(int index)
    {
        var name = string.IsNullOrEmpty(DisplayName) ? string.Empty : DisplayName + ": ";

        return $"{name}{Formatted(ChartDimension.X, index)}, {Formatted(ChartDimension.Y, index)}";
    }

    private string Formatted(ChartDimension dimension, int index)
    {
        var unit = UnitAt(dimension, index);

        return double.IsNaN(unit) || AxisFor(dimension) is not { } axis ? "—" : axis.FormatValue(unit);
    }

    private HitData Hit(int index, double distance) => new()
    {
        Distance = distance,
        ChartItem = new ChartItem(this, index),
        DisplayText = Describe(index),
    };

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(((first.X - second.X) * (first.X - second.X)) + ((first.Y - second.Y) * (first.Y - second.Y)));

    /// <inheritdoc />
    protected override void UpdateData()
    {
        StopWatchingItems();

        var source = _itemsSource;

        _count = 0;
        _horizontalAxis = AxisFor(ChartDimension.X);
        _verticalAxis = AxisFor(ChartDimension.Y);

        if (source is null)
        {
            return;
        }

        var position = 0;

        foreach (var item in source)
        {
            if (Measure(position, item, out var x, out var y))
            {
                Store(item, x, y);
                position++;
            }
        }
    }

    /// <inheritdoc />
    public override int ApplyPushed()
    {
        T[] batch;

        lock (_pushGate)
        {
            if (_waiting.Count == 0)
            {
                return 0;
            }

            batch = [.. _waiting];
            _waiting.Clear();
        }

        // The source is told first, and told to keep quiet about it: the values are appended here, in one go, so
        // the records are not measured twice and the batch costs one repaint rather than one a record.
        if (_itemsSource is ICollection<T> collection)
        {
            _applying = true;

            try
            {
                foreach (var item in batch)
                {
                    collection.Add(item);
                }
            }
            finally
            {
                _applying = false;
            }
        }

        // A series whose values are already going to be read from the source will find the records there, so
        // appending them here as well would count them twice.
        if (!DataIsStale)
        {
            foreach (var item in batch)
            {
                if (Measure(_count, item, out var x, out var y))
                {
                    Store(item, x, y);
                }
            }
        }

        InvalidateGeometry();
        return batch.Length;
    }

    /// <inheritdoc />
    protected override void DescribeData(DataDescription description)
    {
        var transform = Transform;

        if (transform is null)
        {
            return;
        }

        var horizontalAxis = AxisFor(ChartDimension.X);
        var verticalAxis = AxisFor(ChartDimension.Y);

        // The values are already the arrays everything else reads, so they are handed over rather than walked:
        // the axis wants the lowest and the highest, and finding those costs one pass and two entries, not one
        // entry a point.
        if (horizontalAxis is not null)
        {
            description.Add(ChartDimension.X, XValues);
        }

        if (verticalAxis is not null)
        {
            description.Add(ChartDimension.Y, YValues);
        }
    }

    /// <inheritdoc />
    protected override bool ValuesAreStale
    {
        get
        {
            // The values are measured in the axis' arithmetic, so a dimension that changed axis - a linear scale
            // replaced by a logarithmic one, say - leaves them meaning something else. Reading the source again
            // is what puts that right, because the objects it read are not kept.
            var transform = Transform;

            return !ReferenceEquals(_horizontalAxis, AxisFor(ChartDimension.X))
                || !ReferenceEquals(_verticalAxis, AxisFor(ChartDimension.Y));
        }
    }

    /// <summary>
    /// Reads one record into unit space, saying whether it is kept at all.
    /// </summary>
    /// <param name="position">Its position among the records kept, which is the default horizontal value.</param>
    /// <param name="item">The item.</param>
    /// <param name="x">The horizontal value in unit space; NaN when the record has none.</param>
    /// <param name="y">The vertical value in unit space; NaN when the record has none.</param>
    /// <returns><see langword="false"/> when the filter drops the record, which leaves no slot behind.</returns>
    private bool Measure(int position, T item, out double x, out double y)
    {
        x = double.NaN;
        y = double.NaN;

        // A null item is a record-shaped hole: it keeps its place, like a missing value does, and no selector is
        // asked about it. Keeping it is also what lets a position in the source be a position in the values.
        if (item is null)
        {
            return true;
        }

        if (_filter is not null && !_filter(item))
        {
            return false;
        }

        Watch(item);

        if (_horizontalAxis is not null &&
            _horizontalAxis.TryToUnit(_xSelector is null ? (ChartValue)position : _xSelector(item), out var horizontal))
        {
            x = horizontal;
        }

        if (_verticalAxis is not null &&
            _verticalAxis.TryToUnit(_ySelector is null ? ChartValue.Empty : _ySelector(item), out var vertical))
        {
            y = vertical;
        }

        return true;
    }

    /// <summary>Adds one measured record at the end of the values.</summary>
    private void Store(T item, double x, double y)
    {
        if (_count == _x.Length)
        {
            var capacity = Math.Max(4, _x.Length * 2);
            Array.Resize(ref _x, capacity);
            Array.Resize(ref _y, capacity);
            Array.Resize(ref _items, capacity);
        }

        _x[_count] = x;
        _y[_count] = y;
        _items[_count] = item;
        _ascending = _count == 0 || !(x < _x[_count - 1]);
        _count++;
    }

    private void Watch(T item)
    {
        if (item is INotifyPropertyChanged notifier && _watched.Add(notifier))
        {
            notifier.PropertyChanged += OnItemChanged;
        }
    }

    private void StopWatchingItems()
    {
        foreach (var notifier in _watched)
        {
            notifier.PropertyChanged -= OnItemChanged;
        }

        _watched.Clear();
    }

    private void StartListening(IEnumerable<T>? source)
    {
        _announcing = source as INotifyCollectionChanged;

        if (_announcing is not null)
        {
            _announcing.CollectionChanged += OnCollectionChanged;
        }
    }

    private void StopListening()
    {
        if (_announcing is not null)
        {
            _announcing.CollectionChanged -= OnCollectionChanged;
            _announcing = null;
        }

        // The records are about to be read again, and reading subscribes to whatever it finds; letting go of the
        // ones from last time is what keeps a rolling window from holding on to the objects that left it.
        StopWatchingItems();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        // One item changed, and a series that kept no reference per point cannot say which record it was, so it
        // reads its source again. That is the simple honest answer, and only a source whose items announce
        // themselves pays for it: a chart of plain values costs nothing here.
        InvalidateData();
    }

    /// <summary>
    /// Follows a change to the source, where following it is cheaper than reading the source again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two changes are small: a record added at the end, and a record replaced where it is. Both leave every
    /// other position alone, so both cost the records that changed.
    /// </para>
    /// <para>
    /// Anything else is not small. The horizontal value of a record is its position among the records kept, so
    /// inserting or removing one in the middle moves every position after it. The series reads its source again,
    /// which is the honest thing to do rather than pretend the change was small.
    /// </para>
    /// </remarks>
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A batch of pushes being added to the source is not news: the series is appending those records' values
        // itself, once, and applying them here as well would count them twice and repaint once a record.
        if (_applying)
        {
            return;
        }

        // The values are already going to be read from the source, so this change is covered by that read - and
        // applying it here as well would count it twice.
        if (DataIsStale)
        {
            return;
        }

        var adding = e.NewItems?.Count ?? 0;
        var last = CountOf(_itemsSource);
        var atEnd = e.NewStartingIndex >= 0 && e.NewStartingIndex + adding == last;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when atEnd:
                Append(e.NewItems!);
                return;

            case NotifyCollectionChangedAction.Replace when _filter is null && e.OldStartingIndex >= 0:
                Replace(e.OldStartingIndex, e.NewItems!);
                return;
        }

        InvalidateData();
    }

    /// <summary>Adds records that arrived at the end of the source.</summary>
    private void Append(IList items)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (Measure(_count, (T)items[index]!, out var x, out var y))
            {
                Store((T)items[index]!, x, y);
            }
        }

        InvalidateGeometry();
    }

    /// <summary>Measures records that were replaced, in the places they already had.</summary>
    private void Replace(int from, IList items)
    {
        if (from + items.Count > _count)
        {
            // The change does not line up with the values, which means a filter or a reset happened without the
            // series hearing about it. Read again rather than write somewhere wrong.
            InvalidateData();
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var record = (T)items[index]!;

            if (Measure(from + index, record, out var x, out var y))
            {
                _x[from + index] = x;
                _y[from + index] = y;
                _items[from + index] = record;

                // One record changed, so the index is only as good as the order it is still in.
                _ascending = _ascending && IsInOrder(from + index);
            }
            else
            {
                // The record the filter used to keep is dropped now, which moves everything after it: only a
                // read of the source can say what the values are from there on.
                InvalidateData();
                return;
            }
        }

        InvalidateGeometry();
    }

    private static int CountOf(IEnumerable<T>? source) => source switch
    {
        IReadOnlyCollection<T> readOnly => readOnly.Count,
        ICollection<T> collection => collection.Count,
        _ => -1,
    };

    /// <summary>Whether a record still sits in order between its neighbours.</summary>
    private bool IsInOrder(int index) =>
        (index == 0 || !(_x[index] < _x[index - 1])) &&
        (index == _count - 1 || !(_x[index] > _x[index + 1]));
}
