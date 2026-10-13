namespace AvCharts.Charts;

/// <summary>A series drawn in bands: one shape per record, standing in the slot its value names.</summary>
/// <remarks>
/// The capability the chart's <see cref="BandLayout"/> is built from, so that a custom series can join a cluster
/// by implementing this and nothing else. Flex splits the same idea into <c>IColumn</c> and <c>IBar</c>, with a
/// separate set of property names for each; here it is one interface, because a bar is a column lying on its
/// side - Flex's own <c>BarSeries</c> extends its <c>ColumnSeries</c> - and two names for one setting would be a
/// second thing to keep in step.
/// </remarks>
public interface IColumn
{
    /// <summary>How much of the band a column takes, as a fraction of it.</summary>
    double ColumnWidthRatio { get; }

    /// <summary>The widest a column may be drawn, in logical pixels; unset, only the band limits it.</summary>
    double? MaxColumnWidth { get; }
}

/// <summary>How the series that are laid out in bands - columns, bars - share the space they are given.</summary>
/// <remarks>
/// Flex's <c>ChartBase.type</c>, with the member names the design brief settles on. The difference between the
/// three matters as soon as a chart has two series that both want the same slot: clustered they stand side by
/// side, overlaid they are drawn on top of one another - which is what a column chart with a line over it wants -
/// and stacked they accumulate.
/// </remarks>
public enum SeriesLayout
{
    /// <summary>Side by side within each slot. What two series of columns side by side need.</summary>
    Cluster,

    /// <summary>Each series takes the whole slot and they are drawn in order, so the last is on top.</summary>
    Overlay,

    /// <summary>Each series is drawn on top of the one before it, and its values add to that total.</summary>
    Stacked,
}

/// <summary>What one chart's stacking has reached at each horizontal position, on either side of the base.</summary>
/// <remarks>
/// Flex hands the stacking series a <c>Dictionary</c> and lets them read and write it as they go; this is that,
/// with the two directions kept apart, because a column of negative values stacks downwards. The positions are
/// horizontal unit values, so records stack when their values put them in the same place - which for a band
/// layout is the same slot.
/// </remarks>
public sealed class StackTotals
{
    private readonly Dictionary<double, double> _above = [];
    private readonly Dictionary<double, double> _below = [];

    /// <summary>How far the stack has reached above the base at a horizontal position.</summary>
    /// <param name="unit">The horizontal value, in unit space.</param>
    /// <returns>The height above the base, in unit space.</returns>
    public double Above(double unit) => _above.GetValueOrDefault(unit);

    /// <summary>How far the stack has reached below the base at a horizontal position.</summary>
    /// <param name="unit">The horizontal value, in unit space.</param>
    /// <returns>The depth below the base, in unit space.</returns>
    public double Below(double unit) => _below.GetValueOrDefault(unit);

    /// <summary>Adds a height at a horizontal position, on the side of the base the sign says.</summary>
    /// <param name="unit">The horizontal value, in unit space.</param>
    /// <param name="height">The height above the base; negative stacks downwards.</param>
    public void Add(double unit, double height)
    {
        if (height >= 0)
        {
            _above[unit] = Above(unit) + height;
        }
        else
        {
            _below[unit] = Below(unit) + height;
        }
    }
}

/// <summary>A series whose values add to those of the series beneath it.</summary>
/// <remarks>
/// The design brief's extension contract for stacking, as Flex has it: the chart walks its series in order and
/// folds each of them onto the totals the ones below have left, so a series at the bottom of a stack and one at
/// the top are the same code with a different total handed in.
/// </remarks>
public interface IStackable
{
    /// <summary>Folds this series onto the totals, which is what the series draws its base from.</summary>
    /// <param name="totals">What the series below have accumulated; this series adds its own values to it.</param>
    void Stack(StackTotals totals);
}

/// <summary>
/// Where the band-laid series of one chart sit: which of them share a slot, in what order, and how they are
/// arranged within it.
/// </summary>
/// <remarks>
/// <para>
/// A result of the chart's layout rather than a setting, and handed to the series with the plot rectangle and
/// the transform - see <see cref="SeriesRenderContext.Bands"/>. A cluster is the only mode in which a series
/// shares its slot, so a series asks this where to stand rather than working it out from anything of its own,
/// which is what keeps two series from overlapping by accident.
/// </para>
/// <para>
/// Only the series that are laid out in bands are in here. A line drawn over a column chart is not one of them
/// and takes no room from the columns, which is what makes a combination chart possible at all.
/// </para>
/// </remarks>
public sealed class BandLayout
{
    private readonly List<Series> _series = [];

    /// <summary>Creates a layout for one chart.</summary>
    /// <param name="mode">How the band series share a slot.</param>
    public BandLayout(SeriesLayout mode) => Mode = mode;

    /// <summary>How the band series share a slot.</summary>
    public SeriesLayout Mode { get; }

    /// <summary>How many series are laid out in bands, in the order they were added to the chart.</summary>
    public int Count => _series.Count;

    /// <summary>The series in the layout, in the order they were added to the chart.</summary>
    public IReadOnlyList<Series> Series => _series;

    /// <summary>Registers a series, which the chart does as it walks its own list.</summary>
    /// <param name="series">The series.</param>
    public void Add(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (!_series.Contains(series))
        {
            _series.Add(series);
        }
    }

    /// <summary>
    /// Where a series stands within a slot: which of the series sharing it this is, and how many there are.
    /// </summary>
    /// <param name="series">The series; one that is not in this layout gets the slot to itself.</param>
    /// <returns>The series' place, counted from zero.</returns>
    public (int Index, int Count) PlaceOf(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (Mode != SeriesLayout.Cluster)
        {
            // Overlaid or stacked, every series has the whole slot to itself; the order they are drawn in is the
            // order they are in, which is the chart's business rather than this.
            return (0, 1);
        }

        var index = _series.IndexOf(series);

        return index < 0 ? (0, 1) : (index, _series.Count);
    }
}
