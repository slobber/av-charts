using Avalonia;
using Avalonia.Media;


using AvCharts.Data;



namespace AvCharts.Series;

/// <summary>
/// A series drawn from the four prices of a record: where it opened, the highest and the lowest it reached, and
/// where it closed.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// Four prices to a record, which is what a bar of a price series is: the close is the record's own value - it is
/// what <see cref="CartesianSeries{T}.YSelector"/> holds, and what the series sets when
/// <see cref="CloseSelector"/> is given - and the open, the high and the low are the series' own. A price that is
/// not given is read as the close, so a series given nothing but a close draws a line of flat shapes rather than
/// nothing at all: a price that a feed reports one number of is still a price.
/// </para>
/// <para>
/// What the shape is belongs to the series below this one, and it is the only thing that does. A candle draws a
/// body from the open to the close with a wick reaching the high and the low; a high-low-close bar draws one
/// line from the low to the high with a tick each way at the open and at the close. The four prices, the axis
/// that covers them, the two inks a rising and a falling bar are drawn in, and the pointer that finds the bar
/// under it are the same for both families - and were a copy each until the second one arrived.
/// </para>
/// <para>
/// The bands are the ones a column stands in: the readings' own spacing, the fraction of it a bar takes (0.65 by
/// default) and the cap on how wide one may grow. What differs from a column is what the bar reaches: a price bar
/// runs between its own prices rather than from the zero of the axis, so an axis of prices covers the prices and
/// not the distance to a zero none of them is near.
/// </para>
/// </remarks>
public abstract class PriceBarSeries<T> : BandedSeries<T>
{
    private Func<T, ChartValue>? _open;
    private Func<T, ChartValue>? _high;
    private Func<T, ChartValue>? _low;
    private Func<T, ChartValue>? _close;

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

    /// <summary>The fill of a bar that closed at or above where it opened.</summary>
    public IBrush? RiseFill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen around a bar that closed at or above where it opened.</summary>
    public Pen? RiseStroke
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The fill of a bar that closed below where it opened.</summary>
    public IBrush? FallFill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen around a bar that closed below where it opened.</summary>
    public Pen? FallStroke
    {
        get;
        set => Paint(ref field, value);
    }

    /// <inheritdoc />
    public override bool HasOwnColours =>
        RiseFill is not null || RiseStroke is not null || FallFill is not null || FallStroke is not null;

    /// <inheritdoc />
    /// <remarks>The colour the legend shows beside the name: what a rising bar is drawn in.</remarks>
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
        if (AxisFor(BandDimension) is null)
        {
            return;
        }

        EnsureData();

        var x = XValues;
        var y = YValues;

        for (var index = 0; index < x.Length; index++)
        {
            // Half a band of room on each side, so that the first and the last bar are whole.
            DescribeSlot(description, BandDimension == ChartDimension.X ? x[index] : y[index]);

            if (AxisFor(ChartDimension.Y) is null)
            {
                continue;
            }

            // What the axis covers is what a bar reaches: its low and its high, never an axis zero.
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
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        HitData? best = null;
        var along = BandDimension == ChartDimension.X ? point.X : point.Y;

        for (var index = 0; index < _count; index++)
        {
            if (!TryBandEdges(index, out var near, out var far))
            {
                continue;
            }

            var distance = Math.Abs(along - ((near + far) / 2));

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
    protected double Opening(int index) => ReadingOf(index, OpenSelector) is { } value && !double.IsNaN(value)
        ? value
        : Closing(index);

    /// <summary>The highest price of a bar, in the units of the value axis; the body's end when it is not given.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double HighOf(int index) => ReadingOf(index, HighSelector) is { } value && !double.IsNaN(value)
        ? value
        : Math.Max(Opening(index), Closing(index));

    /// <summary>The lowest price of a bar, in the units of the value axis; the body's end when it is not given.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double LowOf(int index) => ReadingOf(index, LowSelector) is { } value && !double.IsNaN(value)
        ? value
        : Math.Min(Opening(index), Closing(index));

    /// <summary>The price a bar closed at, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double Closing(int index) =>
        index >= 0 && index < _count ? YValues[index] : double.NaN;

    /// <summary>Whether a bar closed at or above where it opened, which is the ink it is drawn in.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns><see langword="true"/> when it rose.</returns>
    /// <remarks>
    /// A bar that closed exactly where it opened counts as having risen, which is the convention the two colours
    /// are read by: a price that did not fall is not drawn in the colour of one that did.
    /// </remarks>
    protected bool Rising(int index) => Closing(index) >= Opening(index);

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

        // Ink, and only ink: the shape of a bar is where its prices are, and the colour it wears is put on at
        // draw time - so a host that changes the two colours gets a repaint rather than a chart's worth of work.
        Repaint();
    }
}
