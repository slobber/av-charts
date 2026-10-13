using Avalonia;

namespace AvCharts.Charts;

/// <summary>
/// A series whose two dimensions are the horizontal and vertical axes of a Cartesian chart.
/// </summary>
/// <remarks>
/// <para>
/// The values are read once per change and kept as <see cref="ChartValue"/> pairs, so nothing downstream ever
/// touches the caller's objects: a selector that boxes does it once, not once per frame.
/// </para>
/// <para>
/// A record whose value cannot be placed - missing, of a kind the axis does not understand, or on a dimension
/// with no axis - breaks the run rather than being moved to the origin. That is the null policy the design
/// brief settled on, and the reason a dropped sample in a sensor feed shows up as a break instead of a dive to
/// zero.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="ItemsSource"/>.</typeparam>
public abstract class CartesianSeries<T> : Series
{
    private readonly List<(ChartValue X, ChartValue Y)> _values = [];

    private IEnumerable<T>? _itemsSource;
    private Func<T, ChartValue>? _xSelector;
    private Func<T, ChartValue>? _ySelector;
    private Func<T, bool>? _filter;
    private SamplingMode _sampling = SamplingMode.None;

    /// <summary>
    /// The items to show. Assigning a new source re-reads it; a source that changes underneath -
    /// an observable collection, a mutable list - is handled in the data pipeline step, not here.
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

            _itemsSource = value;
            InvalidateData();
        }
    }

    /// <summary>
    /// How to read the horizontal value of an item. Left unset, the value is the item's index, which is what a
    /// line of plain values wants.
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
    /// Which records to drop. Dropping a record removes it from the series altogether - it takes up no slot
    /// and counts for nothing - whereas a missing value keeps its place and is only drawn as a gap.
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
    /// The values currently on the series: the only thing the drawing code reads. Reading them brings the
    /// source up to date first, so a subclass never works from data that moved under it.
    /// </summary>
    /// <remarks>A subclass must not read this from <see cref="Series.UpdateData"/>; there is nothing there yet.</remarks>
    protected IReadOnlyList<(ChartValue X, ChartValue Y)> Values
    {
        get
        {
            EnsureData();
            return _values;
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
        List<Point>? current = null;

        foreach (var (x, y) in Values)
        {
            if (context.TryPoint(x, y, out var point))
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
        _values.Clear();

        if (_itemsSource is null)
        {
            return;
        }

        var index = 0;

        foreach (var item in _itemsSource)
        {
            var position = index++;

            if (item is null)
            {
                continue;
            }

            if (_filter is not null && !_filter(item))
            {
                continue;
            }

            var x = _xSelector is null ? (ChartValue)position : _xSelector(item);
            var y = _ySelector is null ? ChartValue.Empty : _ySelector(item);

            _values.Add((x, y));
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

        foreach (var (x, y) in _values)
        {
            if (horizontalAxis is not null && horizontalAxis.TryToUnit(x, out var horizontalUnit))
            {
                description.Add(ChartDimension.X, horizontalUnit);
            }

            if (verticalAxis is not null && verticalAxis.TryToUnit(y, out var verticalUnit))
            {
                description.Add(ChartDimension.Y, verticalUnit);
            }
        }
    }
}
