using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Readings drawn as high-low-close bars: one bar per record, standing in its own band, drawn as a line from the
/// low to the high with a tick each way at the open and at the close.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// The shape is Flex's <c>mx.charts.renderers.HLOCItemRenderer</c>: a line up the middle of the band from the low
/// to the high, the open tick reaching from it to the left and the close tick to the right, each of them half a
/// band long - Flex's <c>w2 = width / 2</c>, which is also what clamps a tick that would be longer. The three
/// pieces meet at one column of the pixel grid, so a bar reads as one shape rather than three that nearly touch.
/// </para>
/// <para>
/// Two deliberate differences from Flex. Flex's default stroke for the middle line is fully transparent, so a
/// chart left to itself shows two ticks and hides where the prices are; the line is drawn here, because a chart is
/// read by looking at it. And Flex draws every high-low-close bar in one colour, where the colour is free to say
/// which way the price went - here the whole bar takes the rise or the fall colour, which is the one thing a
/// high-low-close bar does less well than a candle and need not.
/// </para>
/// <para>
/// Like a candle, the bar is not one of the series a chart lays out in bands, so it stands in the whole of its
/// slot. Flex's own <c>HLOCSeriesBase</c> implements its <c>IColumn</c>, which would put two price bars of the
/// same readings side by side; nobody has asked for that here, and a bar that joined a cluster would take a
/// fraction of the band its ticks are measured against.
/// </para>
/// </remarks>
public class HighLowCloseSeries<T> : PriceBarSeries<T>
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
                if (!TryBar(index, position, in context, facts.Width, facts.Shift, out var needle, out var openTick, out var closeTick))
                {
                    continue;
                }

                var batch = Rising(index) ? rising : falling;

                batch.AddRectangle(needle);
                batch.AddRectangle(openTick);
                batch.AddRectangle(closeTick);
            }
        }

        _rising = rising.Build();
        _falling = falling.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var rising = InkOf(RiseStroke, RiseFill, PaletteFill ?? PaletteStroke?.Brush);
        var falling = InkOf(FallStroke, FallFill, rising);

        if (_rising is not null && rising is not null)
        {
            context.DrawGeometry(rising, null, _rising);
        }

        if (_falling is not null && falling is not null)
        {
            context.DrawGeometry(falling, null, _falling);
        }
    }

    /// <summary>Works out one bar's line and its two ticks, in the chart's own coordinates.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="position">Where the record was laid out.</param>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="width">How wide a bar is, in pixels.</param>
    /// <param name="shift">How far the series stands from the middle of the band.</param>
    /// <param name="needle">The line from the low to the high, when this returns <see langword="true"/>.</param>
    /// <param name="openTick">The tick at the opening price.</param>
    /// <param name="closeTick">The tick at the closing price.</param>
    /// <returns><see langword="false"/> when the record draws no bar.</returns>
    private bool TryBar(
        int index,
        Point position,
        in SeriesRenderContext context,
        double width,
        double shift,
        out Rect needle,
        out Rect openTick,
        out Rect closeTick)
    {
        needle = default;
        openTick = default;
        closeTick = default;

        var x = XValues[index];
        var close = Closing(index);

        if (double.IsNaN(close) || !TryPoint(x, Opening(index), out var openAt) ||
            !TryPoint(x, close, out var closeAt) || !TryPoint(x, HighOf(index), out var highAt) ||
            !TryPoint(x, LowOf(index), out var lowAt))
        {
            return false;
        }

        // The bar is centred on its band, and the band's edges are snapped to the pixel grid so that a run of them
        // tiles. The middle is one column of that grid rather than the arithmetic centre of the band: the three
        // pieces of a bar meet there, and a hairline has to stand in a column rather than across two of them.
        var edge = Math.Max(context.Snap(width), 1);
        var left = Math.Round(context.Snap(position.X + shift) - (edge / 2));
        var middle = Math.Round(left + ((edge - 1) / 2));
        var top = Math.Min(highAt.Y, lowAt.Y);
        var bottom = top + Math.Max(Math.Abs(highAt.Y - lowAt.Y), 1);

        // A tick runs from the middle column out to the edge of the band, so the two of them together are as wide
        // as the bar itself. Its row is snapped: a rule laid across two rows of half ink reads as a blur rather
        // than as a price, and the reader is reading a level off the axis.
        needle = new Rect(middle, top, 1, bottom - top);
        openTick = new Rect(left, context.Snap(openAt.Y), (middle - left) + 1, 1);
        closeTick = new Rect(middle, context.Snap(closeAt.Y), (left + edge) - middle, 1);
        return true;
    }

}
