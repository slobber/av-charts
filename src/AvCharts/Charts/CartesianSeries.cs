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
    private double[] _x = [];
    private double[] _y = [];
    private int _count;

    private IEnumerable<T>? _itemsSource;
    private INotifyCollectionChanged? _announcing;
    private readonly HashSet<INotifyPropertyChanged> _watched = new(ReferenceEqualityComparer.Instance);

    private IAxis? _horizontalAxis;
    private IAxis? _verticalAxis;

    private Func<T, ChartValue>? _xSelector;
    private Func<T, ChartValue>? _ySelector;
    private Func<T, bool>? _filter;
    private SamplingMode _sampling = SamplingMode.None;

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

        for (var index = 0; index < x.Length; index++)
        {
            if (context.TryUnitPoint(x[index], y[index], out var point))
            {
                current ??= [];
                current.Add(point);
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

    /// <inheritdoc />
    protected override void UpdateData()
    {
        StopWatchingItems();

        var source = _itemsSource;

        _count = 0;
        _horizontalAxis = Transform?.GetAxis(ChartDimension.X);
        _verticalAxis = Transform?.GetAxis(ChartDimension.Y);

        if (source is null)
        {
            return;
        }

        var position = 0;

        foreach (var item in source)
        {
            if (Measure(position, item, out var x, out var y))
            {
                Store(x, y);
                position++;
            }
        }
    }

    /// <inheritdoc />
    protected override void DescribeData(DataDescription description)
    {
        var transform = Transform;

        if (transform is null)
        {
            return;
        }

        var horizontalAxis = transform.GetAxis(ChartDimension.X);
        var verticalAxis = transform.GetAxis(ChartDimension.Y);

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

            return !ReferenceEquals(_horizontalAxis, transform?.GetAxis(ChartDimension.X))
                || !ReferenceEquals(_verticalAxis, transform?.GetAxis(ChartDimension.Y));
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
    private void Store(double x, double y)
    {
        if (_count == _x.Length)
        {
            var capacity = Math.Max(4, _x.Length * 2);
            Array.Resize(ref _x, capacity);
            Array.Resize(ref _y, capacity);
        }

        _x[_count] = x;
        _y[_count] = y;
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
                Store(x, y);
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
            if (Measure(from + index, (T)items[index]!, out var x, out var y))
            {
                _x[from + index] = x;
                _y[from + index] = y;
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
}
