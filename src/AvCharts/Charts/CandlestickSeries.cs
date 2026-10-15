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
public class CandlestickSeries<T> : BandedSeries<T>
{
    private Func<T, ChartValue>? _open;
    private Func<T, ChartValue>? _high;
    private Func<T, ChartValue>? _low;
    private Func<T, ChartValue>? _close;
    private Geometry? _rising;
    private Geometry? _falling;

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

    /// <summary>Where a bar's opening price is written, as a path; for a markup file.</summary>
    /// <remarks><c>OpenPath</c> is <see cref="OpenSelector"/> for a page.</remarks>
    public string? OpenPath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            OpenSelector = Paths.Value<T>(value);
        }
    }

    /// <summary>Where a bar's highest price is written, as a path.</summary>
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

    /// <summary>Where a bar's lowest price is written, as a path.</summary>
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

    /// <summary>Where a bar's closing price is written, as a path.</summary>
    /// <remarks>
    /// <c>ClosePath</c> is <see cref="CloseSelector"/> for a page, and it sets
    /// <see cref="CartesianSeries{T}.YPath"/> for the same reason that one sets
    /// <see cref="CartesianSeries{T}.YSelector"/> - the close is what the record is worth.
    /// </remarks>
    public string? ClosePath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            CloseSelector = Paths.Value<T>(value);
            YPath = value;
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

        for (var index = 0; index < x.Length; index++)
        {
            // Half a band of room on each side, so that the first and the last candle are whole.
            DescribeSlot(description, x[index]);

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

        if (TryBandFacts(in context, out var facts))
        {
            foreach (var (index, _, _, position) in PlacedRecords(context))
            {
                if (!TryCandle(index, position, in context, facts.Width, facts.Shift, out var body, out var above, out var below))
                {
                    continue;
                }

                var batch = Closing(index) >= Opening(index) ? rising : falling;

                // The wick goes in first, so that a body a pixel tall is drawn over it rather than around it.
                Wick(batch, above);
                Wick(batch, below);
                batch.AddRectangle(body);
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

    /// <summary>Adds one piece of a candle's wick, unless there is no such piece.</summary>
    /// <param name="batch">The batch to add it to.</param>
    /// <param name="piece">The piece.</param>
    /// <remarks>
    /// A piece is empty when the price it would reach lies inside the body - which happens when the body has been
    /// pulled out to its one pixel - and an empty figure is not worth adding: two figures that overlap cancel each
    /// other's fill, because a geometry's own figures are filled by the even-odd rule.
    /// </remarks>
    private static void Wick(GeometryBatch batch, Rect piece)
    {
        if (piece.Height >= 1)
        {
            batch.AddRectangle(piece);
        }
    }

    /// <summary>Works out one candle's body and the two pieces of its wick, in the chart's own coordinates.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="position">Where the record was laid out.</param>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="width">How wide a candle is, in pixels.</param>
    /// <param name="shift">How far the series stands from the middle of the band.</param>
    /// <param name="body">The body, when this returns <see langword="true"/>.</param>
    /// <param name="above">The piece of wick above the body; empty when the high is inside the body.</param>
    /// <param name="below">The piece of wick below the body; empty when the low is inside the body.</param>
    /// <returns><see langword="false"/> when the record draws no candle.</returns>
    private bool TryCandle(
        int index,
        Point position,
        in SeriesRenderContext context,
        double width,
        double shift,
        out Rect body,
        out Rect above,
        out Rect below)
    {
        body = default;
        above = default;
        below = default;

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
        var bodyBottom = bodyTop + Math.Max(Math.Abs(openAt.Y - closeAt.Y), 1);
        var reach = Math.Min(highAt.Y, lowAt.Y);
        var reachEnd = reach + Math.Max(Math.Abs(highAt.Y - lowAt.Y), 1);
        var middle = Math.Round(left + ((edge - 1) / 2));

        // The wick is a hairline down the middle in two pieces, one above the body and one below it, rather than
        // one rectangle the width of the body: a rectangle that wide is a second body, and a rectangle that
        // crossed the body would take the fill out of the middle of it.
        body = new Rect(left, bodyTop, edge, bodyBottom - bodyTop);
        above = new Rect(middle, reach, 1, Math.Max(bodyTop - reach, 0));
        below = new Rect(middle, bodyBottom, 1, Math.Max(reachEnd - bodyBottom, 0));
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
        var width = SlotUnit() * BandWidthRatio * transform.PlotBounds.Width / range.Length;

        if (MaxBandWidth is { } cap && width > cap)
        {
            width = cap;
        }

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
