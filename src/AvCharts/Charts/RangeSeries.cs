using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Readings drawn as bands: one bar per record, reaching from one reading to another.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// Two readings to a record, and the band between them is the whole of the shape - which is what a series of
/// intervals is: an hour's low and high, the spread of a measurement, either end of a confidence interval. The
/// record's own value - what <see cref="CartesianSeries{T}.YSelector"/> holds - is the end left open, so a series
/// given one of the two draws a band from what it was given to what the record already was, and one given
/// neither draws a hairline where the value is rather than nothing at all.
/// </para>
/// <para>
/// The bands are the ones a column stands in, and what differs is what the bar reaches: a range runs between its
/// own two readings rather than from the zero of the axis, so an axis of intervals covers the intervals and not
/// the distance down to a zero none of them is near.
/// </para>
/// <para>
/// Flex has nothing of this shape to copy - its <c>BoxItemRenderer</c> draws a legend swatch and not a band - so
/// the shape is drawn to what the design brief asks for: a filled band per record, with the outline a host may
/// put round it.
/// </para>
/// </remarks>
public class RangeSeries<T> : StatisticalSeries<T>
{
    private Func<T, ChartValue>? _low;
    private Func<T, ChartValue>? _high;
    private Geometry? _bands;

    /// <summary>How to read the low end of a band; unset, the record's own value.</summary>
    public Func<T, ChartValue>? LowSelector
    {
        get => _low;
        set => Read(ref _low, value);
    }

    /// <summary>How to read the high end of a band; unset, the record's own value.</summary>
    public Func<T, ChartValue>? HighSelector
    {
        get => _high;
        set => Read(ref _high, value);
    }

    /// <summary>Where the low end of a band is written, as a path; for a markup file.</summary>
    /// <remarks><c>LowPath</c> is <see cref="LowSelector"/> for a page.</remarks>
    public string? LowPath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            LowSelector = Paths.Value<T>(value);
        }
    }

    /// <summary>Where the high end of a band is written, as a path.</summary>
    /// <remarks><c>HighPath</c> is <see cref="HighSelector"/> for a page.</remarks>
    public string? HighPath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            HighSelector = Paths.Value<T>(value);
        }
    }

    /// <summary>The colour a band is filled with; without one, the chart's palette fills it.</summary>
    public IBrush? Fill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen around a band; without one, a band has no edge.</summary>
    public Pen? Stroke
    {
        get;
        set => Paint(ref field, value);
    }

    /// <inheritdoc />
    public override bool HasOwnColours => Fill is not null || Stroke is not null;

    /// <inheritdoc />
    public override IBrush? Color => Fill ?? base.Color;

    /// <inheritdoc />
    protected override bool TryExtent(int index, out double low, out double high)
    {
        low = LowOf(index);
        high = HighOf(index);
        return !double.IsNaN(low) && !double.IsNaN(high);
    }

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var batch = new GeometryBatch();

        if (TryBandFacts(in context, out var facts))
        {
            // Every record, placed across its slot rather than by a value of its own: an interval is often the
            // only reading a record has - a day and its two temperatures - and waiting for another one would draw
            // nothing at all. See `TryBandStand`.
            for (var index = 0; index < _count; index++)
            {
                if (TryBand(index, in context, in facts, out var band))
                {
                    batch.AddRectangle(band);
                }
            }
        }

        _bands = batch.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var fill = Fill ?? PaletteFill;

        if (_bands is not null && (fill is not null || Stroke is not null))
        {
            context.DrawGeometry(fill, Stroke, _bands);
        }
    }

    /// <summary>The low end of one band, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double LowOf(int index) => ReadingOf(index, LowSelector) is { } value && !double.IsNaN(value)
        ? value
        : ValueOf(index);

    /// <summary>The high end of one band, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double HighOf(int index) => ReadingOf(index, HighSelector) is { } value && !double.IsNaN(value)
        ? value
        : ValueOf(index);

    /// <summary>The record's own value, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double ValueOf(int index) => index >= 0 && index < _count ? YValues[index] : double.NaN;

    /// <summary>Works out one band's rectangle, in the chart's own coordinates.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="facts">How the bands stand in their slot.</param>
    /// <param name="band">The rectangle, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the record draws no band.</returns>
    private bool TryBand(
        int index,
        in SeriesRenderContext context,
        in BandFacts facts,
        out Rect band)
    {
        band = default;

        var x = XValues[index];
        var low = LowOf(index);
        var high = HighOf(index);

        if (double.IsNaN(low) || double.IsNaN(high) || !TryBandStand(index, out var stand) ||
            !TryPoint(x, low, out var lowAt) || !TryPoint(x, high, out var highAt))
        {
            return false;
        }

        // The band is centred on its slot and its sides are snapped to the pixel grid so that a run of them
        // tiles; its two ends stay where the readings put them. A band whose ends are the same reading is drawn
        // a pixel tall rather than not at all.
        var edge = Math.Max(context.Snap(facts.Width), 1);
        var left = Math.Round(context.Snap(stand + facts.Shift) - (edge / 2));
        var top = Math.Min(lowAt.Y, highAt.Y);
        var bottom = Math.Max(lowAt.Y, highAt.Y);

        band = new Rect(left, top, edge, Math.Max(bottom - top, 1));
        return true;
    }

    /// <summary>Sets a selector, reading the source again only when it is a different one.</summary>
    /// <param name="field">The selector being set.</param>
    /// <param name="value">The selector to set.</param>
    private void Read(ref Func<T, ChartValue>? field, Func<T, ChartValue>? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        InvalidateData();
    }

    /// <summary>Sets one of the two inks, asking for a repaint only when it is a different one.</summary>
    /// <typeparam name="TInk">The type of ink.</typeparam>
    /// <param name="field">The ink being set.</param>
    /// <param name="value">The ink to set.</param>
    private void Paint<TInk>(ref TInk? field, TInk? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;

        // Ink, and only ink: the shape of a band is where its readings are, and the colour it wears is put on at
        // draw time - so a host that changes the two gets a repaint rather than a chart's worth of work.
        Repaint();
    }
}
