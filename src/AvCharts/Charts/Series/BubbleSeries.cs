





namespace AvCharts.Series;

/// <summary>
/// A plot whose marks are sized one by one: a third value decides how big each of them is.
/// </summary>
/// <remarks>
/// <para>
/// Flex scales the radius through an axis of its own, the way it scales the other two, which is what a chart of
/// bubbles as areas is doing. There is no radius axis here yet - that arrives with the polar work - so the
/// selector gives the radius in logical pixels and the caller does the arithmetic. It is the one thing about this
/// series that is a simplification rather than a decision.
/// </para>
/// <para>
/// A record whose radius cannot be read is drawn at <see cref="PlotSeries{T}.MarkerSize"/>, so a hole in the
/// third value costs the size of one mark rather than the whole point.
/// </para>
/// <para>
/// Its marks are circles, outlined in the colour they are filled with, which is the one shape that says "this is
/// a size" rather than "this is a point"; a host that wants another shape sets
/// <see cref="PlotSeries{T}.Marker"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class BubbleSeries<T> : PlotSeries<T>
{
    private Func<T, double>? _radiusSelector;

    /// <summary>How big each mark is, as a radius in logical pixels; unset, every mark is the marker size.</summary>
    public Func<T, double>? RadiusSelector
    {
        get => _radiusSelector;
        set
        {
            if (ReferenceEquals(_radiusSelector, value))
            {
                return;
            }

            _radiusSelector = value;
            InvalidateGeometry();
        }
    }

    /// <summary>Where the size of a bubble is written, as a path; for a markup file.</summary>
    /// <remarks><c>RadiusPath</c> is <see cref="RadiusSelector"/> for a page.</remarks>
    public string? RadiusPath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            RadiusSelector = Paths.Number<T>(value);
        }
    }

    /// <inheritdoc />
    protected override double RadiusOf(int index, in SeriesRenderContext context) =>
        RadiusSelector is { } selector && index >= 0 && index < _count && _items[index] is T item
            ? selector(item)
            : MarkerSize;
}
