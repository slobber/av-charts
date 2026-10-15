using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Readings drawn as candles: one bar per record, standing in its own band, with a body from the open to the
/// close and a wick from the low to the high.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// Four prices to a record, which is what a bar of a price series is: the close is the record's value - it is what
/// <see cref="CartesianSeries{T}.YSelector"/> holds, and what the series sets when <see cref="CloseSelector"/> is
/// given - and the open, the high and the low are the series' own. A price that is not given is read as the close,
/// so a series given nothing but a close draws a line of flat candles rather than nothing at all.
/// </para>
/// <para>
/// The bands are the ones a column stands in and the widths are worked out the same way: the readings' own
/// spacing, the fraction of it a bar takes (0.65 by default) and the cap on how wide one may grow. What differs is
/// what stands in the band: a candle reaches from its low to its high rather than from the zero of the axis, so an
/// axis of prices covers the prices and not the distance to a zero none of them is near.
/// </para>
/// </remarks>
public class CandlestickSeries<T> : CartesianSeries<T>
{
    /// <summary>How much of a band a candle takes when it has the band to itself; Flex's own default.</summary>
    public const double DefaultBandWidthRatio = 0.65;

    private Func<T, ChartValue>? _open;
    private Func<T, ChartValue>? _high;
    private Func<T, ChartValue>? _low;
    private Func<T, ChartValue>? _close;
    private double _ratio = DefaultBandWidthRatio;
    private double? _cap;
    private Geometry? _rising;
    private Geometry? _falling;
    private double _slot;

    /// <summary>How to read the price a bar opened at; unset, the close is taken for it.</summary>
    public Func<T, ChartValue>? OpenSelector
    {
        get => _open;
        set => Read(ref _open, value);
    }

    /// <summary>How to read the highest price of a bar; unset, the close is taken for it.</summary>
    public Func<T, ChartValue>? HighSelector
    {
        get => _high;
        set => Read(ref _high, value);
    }

    /// <summary>How to read the lowest price of a bar; unset, the close is taken for it.</summary>
    public Func<T, ChartValue>? LowSelector
    {
        get => _low;
        set => Read(ref _low, value);
    }

    /// <summary>The price a bar closed at, which is the record's own value.</summary>
    /// <remarks>
    /// Setting it sets <see cref="CartesianSeries{T}.YSelector"/> too, because the close is what a record is worth
    /// on the value axis: the band it stands in, the axis that covers it and the tip that reads it are all about
    /// this number. A host that sets <c>YSelector</c> itself is asking for another number to be the record's value.
    /// </remarks>
    public Func<T, ChartValue>? CloseSelector
    {
        get => _close;
        set
        {
            if (ReferenceEquals(_close, value))
            {
                return;
            }

            _close = value;
            YSelector = value;
        }
    }

    /// <summary>How much of the band between two readings a candle takes, as a fraction; 0.65 by default.</summary>
    public double BandWidthRatio
    {
        get => _ratio;
        set
        {
            if (_ratio.Equals(value))
            {
                return;
            }

            _ratio = value;
            InvalidateGeometry();
        }
    }

    /// <summary>How wide a candle may grow, in logical pixels; null, and the fraction of the band decides.</summary>
    public double? MaxBandWidth
    {
        get => _cap;
        set
        {
            if (Nullable.Equals(_cap, value))
            {
                return;
            }

            _cap = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The fill of a candle that closed at or above where it opened.</summary>
    public IBrush? RiseFill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen around a candle that closed at or above where it opened.</summary>
    public Pen? RiseStroke
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The fill of a candle that closed below where it opened.</summary>
    public IBrush? FallFill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen around a candle that closed below where it opened.</summary>
    public Pen? FallStroke
    {
        get;
        set => Paint(ref field, value);
    }

    /// <inheritdoc />
    public override bool HasOwnColours =>
        RiseFill is not null || RiseStroke is not null || FallFill is not null || FallStroke is not null;

    /// <inheritdoc />
    /// <remarks>The colour the legend shows beside the name: what a rising candle is drawn in.</remarks>
    public override IBrush? Color => RiseFill ?? base.Color;

    /// <inheritdoc />
    public override void TakePriceColours(IBrush rise, IBrush fall)
    {
        // A series dressed in colours of its own keeps them: what a host says about a series it made is truer
        // than what the theme says about every series of this kind.
        if (HasOwnColours)
        {
            return;
        }

        RiseFill = rise;
        FallFill = fall;
        RiseStroke = new Pen(rise, 1);
        FallStroke = new Pen(fall, 1);
    }

    /// <inheritdoc />
    protected override void DescribeData(DataDescription description)
    {
        if (AxisFor(ChartDimension.X) is null)
        {
            return;
        }

        EnsureData();

        var x = XValues;

        _slot = MeasureSlot(x);

        for (var index = 0; index < x.Length; index++)
        {
            if (!double.IsNaN(x[index]))
            {
                // Half a band of room on each side, so that the first and the last candle are whole.
                description.Add(ChartDimension.X, new BoundedValue(x[index], _slot / 2, _slot / 2));
            }

            if (AxisFor(ChartDimension.Y) is null)
            {
                continue;
            }

            // What the axis covers is what a candle reaches: its low and its high, never an axis zero.
            foreach (var value in new[] { LowOf(index), HighOf(index) })
            {
                if (!double.IsNaN(value))
                {
                    description.Add(ChartDimension.Y, value);
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var rising = new GeometryBatch();
        var falling = new GeometryBatch();

        if (TryBand(in context, out var width, out var shift))
        {
            foreach (var (index, _, _, position) in PlacedRecords(context))
            {
                if (TryCandle(index, position, in context, width, shift, out var body, out var wick))
                {
                    (Closing(index) >= Opening(index) ? rising : falling).AddRectangle(wick);
                    (Closing(index) >= Opening(index) ? rising : falling).AddRectangle(body);
                }
            }
        }

        _rising = rising.Build();
        _falling = falling.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        if (_rising is not null)
        {
            context.DrawGeometry(RiseFill ?? PaletteFill, RiseStroke, _rising);
        }

        if (_falling is not null)
        {
            context.DrawGeometry(FallFill ?? RiseFill ?? PaletteFill, FallStroke ?? RiseStroke, _falling);
        }
    }

    /// <inheritdoc />
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        HitData? best = null;

        for (var index = 0; index < _count; index++)
        {
            if (!TryBandOf(index, out var near, out var far))
            {
                continue;
            }

            var distance = Math.Abs(point.X - ((near + far) / 2));

            if (distance <= ((far - near) / 2) + sensitivity && (best is not { } found || distance < found.Distance))
            {
                best = Hit(index, distance);
            }
        }

        return best;
    }

    /// <summary>The price a bar opened at, in the units of the value axis; the close when it is not given.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    private double Opening(int index) => Price(index, OpenSelector) is { } value && !double.IsNaN(value)
        ? value
        : Closing(index);

    /// <summary>The highest price of a bar, in the units of the value axis; the body's end when it is not given.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    private double HighOf(int index) => Price(index, HighSelector) is { } value && !double.IsNaN(value)
        ? value
        : Math.Max(Opening(index), Closing(index));

    /// <summary>The lowest price of a bar, in the units of the value axis; the body's end when it is not given.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    private double LowOf(int index) => Price(index, LowSelector) is { } value && !double.IsNaN(value)
        ? value
        : Math.Min(Opening(index), Closing(index));

    /// <summary>The price a bar closed at, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    private double Closing(int index) =>
        index >= 0 && index < _count ? YValues[index] : double.NaN;

    /// <summary>Reads one of a bar's prices, through the selector that reads it.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="selector">The selector, or null when the price was not given.</param>
    /// <returns>The value, in unit space; NaN when it cannot be read.</returns>
    /// <remarks>
    /// Read on demand rather than kept in an array of its own, which is how the bubble series reads its radius and
    /// for the same reason: a second array would have to be trimmed in step with the values, and that is a second
    /// place for the two to disagree. It costs one selector call per price the series was not already given.
    /// </remarks>
    private double Price(int index, Func<T, ChartValue>? selector) =>
        selector is not null && AxisFor(ChartDimension.Y) is { } axis && index >= 0 && index < _count
        && _items[index] is T item && selector(item) is { IsEmpty: false } value
        && axis.TryToUnit(value, out var unit)
            ? unit
            : double.NaN;

    /// <summary>Works out how wide the band is and where this series stands in it.</summary>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="width">How wide one candle is, when this returns <see langword="true"/>.</param>
    /// <param name="shift">How far the series' share of the band stands from its middle.</param>
    /// <returns><see langword="false"/> when there is no band for a candle to stand in.</returns>
    private bool TryBand(in SeriesRenderContext context, out double width, out double shift)
    {
        width = 0;
        shift = 0;

        var plot = context.PlotBounds;

        if (AxisFor(ChartDimension.X) is not { } axis || !(plot.Width > 0))
        {
            return false;
        }

        var range = context.Transform.GetLayout(axis).UnitRange;

        if (range.IsEmpty)
        {
            return false;
        }

        var (slot, slots) = context.Bands?.PlaceOf(this) ?? (0, 1);
        var band = Band();
        var whole = Width(band, plot.Width / range.Length);

        width = whole / slots;

        if (!(width > 0))
        {
            return false;
        }

        shift = ((slot + 0.5) * width) - (whole / 2);
        return true;
    }

    /// <summary>How wide one band is, in unit space; one unit when the readings cannot say.</summary>
    /// <returns>The band.</returns>
    private double Band() => SlotUnit() * BandWidthRatio;

    /// <summary>The width a candle is actually drawn at, given what a band is worth in pixels.</summary>
    /// <param name="band">How much of a band a candle takes, in unit space.</param>
    /// <param name="pixelsPerUnit">How many pixels a unit is worth.</param>
    /// <returns>The width, in pixels.</returns>
    private double Width(double band, double pixelsPerUnit)
    {
        var width = band * pixelsPerUnit;

        return _cap is { } cap && width > cap ? cap : width;
    }

    /// <summary>How wide one band is in unit space, read off the readings' own spacing.</summary>
    /// <returns>The band; one unit when there is none to read.</returns>
    private double SlotUnit() => _slot > 0 ? _slot : MeasureSlot(XValues);

    /// <summary>The smallest distance between neighbouring readings, which is what a band is worth.</summary>
    /// <param name="x">The horizontal values.</param>
    /// <returns>The distance; one unit when there is none to read.</returns>
    private static double MeasureSlot(ReadOnlySpan<double> x)
    {
        var smallest = double.PositiveInfinity;

        for (var index = 1; index < x.Length; index++)
        {
            var gap = Math.Abs(x[index] - x[index - 1]);
            smallest = gap > 0 && gap < smallest ? gap : smallest;
        }

        return double.IsPositiveInfinity(smallest) ? 1 : smallest;
    }

    /// <summary>Works out one candle's body and wick, in the chart's own coordinates.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="position">Where the record was laid out.</param>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="width">How wide a candle is, in pixels.</param>
    /// <param name="shift">How far the series stands from the middle of the band.</param>
    /// <param name="body">The body, when this returns <see langword="true"/>.</param>
    /// <param name="wick">The wick.</param>
    /// <returns><see langword="false"/> when the record draws no candle.</returns>
    private bool TryCandle(
        int index,
        Point position,
        in SeriesRenderContext context,
        double width,
        double shift,
        out Rect body,
        out Rect wick)
    {
        body = default;
        wick = default;

        var x = XValues[index];
        var close = Closing(index);

        if (double.IsNaN(close) || !TryPoint(x, Opening(index), out var openAt) ||
            !TryPoint(x, close, out var closeAt) || !TryPoint(x, HighOf(index), out var highAt) ||
            !TryPoint(x, LowOf(index), out var lowAt))
        {
            return false;
        }

        // The candle is centred on its band, and the band edges are snapped to the pixel grid so that a run of
        // them tiles; the ends of the body stay where the prices put them.
        var edge = Math.Max(context.Snap(width), 1);
        var left = Math.Round(context.Snap(position.X + shift) - (edge / 2));
        var bodyTop = Math.Min(openAt.Y, closeAt.Y);
        var wickTop = Math.Min(highAt.Y, lowAt.Y);

        body = new Rect(left, bodyTop, edge, Math.Max(Math.Abs(openAt.Y - closeAt.Y), 1));
        wick = new Rect(left, wickTop, edge, Math.Max(Math.Abs(highAt.Y - lowAt.Y), 1));
        return true;
    }

    /// <summary>Where one candle's band stands in the chart's own coordinates, for a pointer to be answered in.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="near">Where the band starts, when this returns <see langword="true"/>.</param>
    /// <param name="far">Where it ends.</param>
    /// <returns><see langword="false"/> when the record has no band to stand in.</returns>
    /// <remarks>
    /// A hit test happens between frames, with no layout to work the band out in, so it is worked out from the
    /// values the series is holding - the same way, and in the same place, as the geometry pass works it out.
    /// </remarks>
    private bool TryBandOf(int index, out double near, out double far)
    {
        near = 0;
        far = 0;

        if (Transform is not { } transform)
        {
            return false;
        }

        var x = XValues[index];
        var range = transform.GetLayout(ChartDimension.X).UnitRange;
        var width = Width(Band(), transform.PlotBounds.Width / range.Length);

        if (double.IsNaN(x) || range.IsEmpty || !(width > 0) || !TryPoint(x, 0, out var position))
        {
            return false;
        }

        near = position.X - (width / 2);
        far = position.X + (width / 2);
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

    /// <summary>Sets one of the four inks, asking for a repaint only when it is a different one.</summary>
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

        // Ink, and only ink: the shape of a candle is where its prices are, and the colour it wears is put on at
        // draw time - so a host that changes the two colours gets a repaint rather than a chart's worth of work.
        Repaint();
    }
}
