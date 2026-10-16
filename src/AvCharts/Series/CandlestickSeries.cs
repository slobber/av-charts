using Avalonia;
using Avalonia.Media;

using AvCharts.Drawing;




namespace AvCharts.Series;

/// <summary>
/// Readings drawn as candles: one bar per record, standing in its own band, with a body from the open to the
/// close and a wick from the low to the high.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// The four prices, the band a candle stands in, the axis that covers it, the two colours a rising and a falling
/// candle are drawn in and the pointer that finds one are a <see cref="PriceBarSeries{T}"/>'s. What is here is
/// the shape they are drawn as: a body between the open and the close, and a hairline wick reaching the high and
/// the low.
/// </para>
/// <para>
/// The wick is two pieces - one above the body and one below it - rather than one rectangle the width of the
/// body: a rectangle that wide is a second body, and a rectangle that crossed the body would be ink in the middle
/// of it. They are drawn in the one batch, which adds shapes up rather than cancelling them.
/// </para>
/// </remarks>
public class CandlestickSeries<T> : PriceBarSeries<T>
{
    private Geometry? _rising;
    private Geometry? _falling;

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

                var batch = Rising(index) ? rising : falling;

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

    /// <summary>Adds one piece of a candle's wick, unless there is no such piece.</summary>
    /// <param name="batch">The batch to add it to.</param>
    /// <param name="piece">The piece.</param>
    /// <remarks>
    /// A piece is empty when the price it would reach lies inside the body - which happens when the body has been
    /// pulled out to its one pixel - and an empty figure is nothing to draw, so it is not added.
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
}
