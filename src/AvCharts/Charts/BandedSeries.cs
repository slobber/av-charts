namespace AvCharts.Charts;

/// <summary>
/// A series whose records stand in bands: the space one slot covers.
/// </summary>
/// <remarks>
/// <para>
/// The shape of a column, a bar, a candle, a box or a range is a <em>band</em> - the space one record's slot
/// covers. On a <see cref="CategoryAxis"/> that is one unit, because there the unit space <em>is</em> the slots;
/// on an axis that measures something - a date, a number - a unit means nothing in particular and the slot is
/// what the readings' own spacing says, so a day of readings on a <see cref="DateTimeAxis"/> gives a day-wide
/// column rather than one a thousandth of a millisecond wide. The size comes from the layout rather than from
/// the axis: an axis holds settings and not results, and one axis can drive several charts of different widths.
/// </para>
/// <para>
/// Where a series stands within its band is the chart's decision rather than the series': see
/// <see cref="BandLayout"/>. A series that is not in a chart, or is in one with no band layout, takes the whole
/// band.
/// </para>
/// <para>
/// This is the arithmetic of that, in one place rather than once per family: the column and the candle each had
/// their own copy of it, and the third family - the statistics - was the point at which two copies became three.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public abstract class BandedSeries<T> : CartesianSeries<T>
{
    /// <summary>How much of a band a shape takes when it has the band to itself; Flex's own default.</summary>
    public const double DefaultBandWidthRatio = 0.65;

    private double _ratio = DefaultBandWidthRatio;
    private double? _cap;
    private double _slot;

    /// <summary>Which dimension the bands run along: across for a column, up for a bar.</summary>
    protected virtual ChartDimension BandDimension => ChartDimension.X;

    /// <summary>How much of the band between two records a shape takes, as a fraction; 0.65 by default.</summary>
    /// <remarks>
    /// What is actually drawn is the smaller of this fraction of the band and <see cref="MaxBandWidth"/>, which
    /// is Flex's rule: the fraction keeps a shape in proportion to its slot as a chart is resized, and the cap
    /// stops a chart of three categories from drawing three absurdly wide columns.
    /// </remarks>
    public double BandWidthRatio
    {
        get => _ratio;
        set
        {
            if (!(value > 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A shape is a fraction of its band, so the fraction has to be above zero.");
            }

            if (_ratio.Equals(value))
            {
                return;
            }

            _ratio = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The widest a shape may be drawn, in logical pixels; unset, only the band limits it.</summary>
    public double? MaxBandWidth
    {
        get => _cap;
        set
        {
            if (value is { } cap && !(cap > 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A cap of zero or less would draw nothing at all, which is a mistake rather than a setting.");
            }

            if (Nullable.Equals(_cap, value))
            {
                return;
            }

            _cap = value;
            InvalidateGeometry();
        }
    }

    /// <summary>How wide a shape is, and how far it stands from its record's own place.</summary>
    /// <param name="Width">The width of one shape, in pixels.</param>
    /// <param name="Shift">How far a shape stands from the record's place, in pixels.</param>
    private protected readonly record struct BandFacts(double Width, double Shift);

    /// <summary>Works out how this series' shapes stand in the band, or says they cannot be drawn.</summary>
    /// <param name="context">Where the plot is and which chart this is.</param>
    /// <param name="facts">What the shapes are, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when there is no band for a shape to stand in.</returns>
    private protected bool TryBandFacts(in SeriesRenderContext context, out BandFacts facts)
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

        // The band is one slot, and this series gets its share of the fraction of it a shape takes.
        var band = SlotUnit() * length / range.Length * BandWidthRatio;
        var share = band / slots;

        if (!(share > 0))
        {
            return false;
        }

        // The cluster is centred on the slot, and this series stands in the middle of its own share of it - where
        // its share is, not where the shape drawn in it ends up: a cap makes a shape narrower than its share, and
        // a shape that moved left because it had been made narrower would be a cap that also moved the column.
        var width = MaxBandWidth is { } cap && share > cap ? cap : share;

        facts = new BandFacts(width, ((slot + 0.5) * share) - (band / 2));
        return true;
    }

    /// <summary>Declares the room a record's slot needs on the band dimension.</summary>
    /// <param name="description">The description being built.</param>
    /// <param name="value">The record's value on the band dimension, in unit space.</param>
    /// <remarks>
    /// Half a band on each side, so that the first and the last shape are whole: a chart whose window ended at
    /// the last record would cut that record's shape in half.
    /// </remarks>
    private protected void DescribeSlot(DataDescription description, double value)
    {
        _slot = MeasureSlot();

        if (!double.IsNaN(value))
        {
            description.Add(BandDimension, new BoundedValue(value, _slot / 2, _slot / 2));
        }
    }

    /// <summary>
    /// How wide one slot is, in unit space: what a shape stands in.
    /// </summary>
    /// <returns>The slot; one unit when the readings cannot say.</returns>
    /// <remarks>
    /// Worked out as the readings are described, which is when the values last changed, and kept from there: a
    /// pointer is answered between frames and a hit test must not walk the data to find out how wide a shape is.
    /// </remarks>
    private protected double SlotUnit() => _slot > 0 ? _slot : MeasureSlot();

    /// <summary>The width of one slot, read off the readings' own spacing.</summary>
    /// <returns>The smallest distance between neighbouring records; one unit when there is none to read.</returns>
    /// <remarks>
    /// <para>
    /// A category axis is a different matter and is not asked: its unit space is the slots themselves, so one
    /// unit is one slot by construction and the readings' positions say nothing about how wide a slot is.
    /// </para>
    /// <para>
    /// Otherwise the distance between two records is the reader's own idea of how wide a shape should be. A day
    /// of readings on a date axis is a day; readings a thousand apart in some unit of their own are a thousand
    /// apart. Readings that do not run in order have no such spacing to read - a pair that is next in the list
    /// may be nowhere near each other in value - so they keep the old answer of one unit rather than make a
    /// shape the width of the whole series.
    /// </para>
    /// </remarks>
    private protected double MeasureSlot()
    {
        if (AxisFor(BandDimension)?.ValueKind == ChartValueKind.Category)
        {
            return 1;
        }

        var values = BandDimension == ChartDimension.X ? XValues : YValues;
        var slot = double.PositiveInfinity;
        var previous = double.NaN;

        foreach (var value in values)
        {
            if (double.IsNaN(value))
            {
                continue;
            }

            if (!double.IsNaN(previous))
            {
                var gap = Math.Abs(value - previous);

                // Readings that do not run in order have no spacing to read between them - a pair that is
                // next in the list may be nowhere near each other in value - so a series like that keeps the
                // old answer of one unit rather than making a shape the width of the whole series.
                if (gap < 0)
                {
                    return 1;
                }

                if (gap > 0 && gap < slot)
                {
                    slot = gap;
                }
            }

            previous = value;
        }

        return double.IsFinite(slot) ? slot : 1;
    }
}
