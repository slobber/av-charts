using Avalonia;


using AvCharts.Data;
using AvCharts.Transforms;


namespace AvCharts.Series;

/// <summary>
/// What a series is handed when it paints: where the plot is, how to turn values into positions, and how to
/// line a stroke up with the pixel grid.
/// </summary>
/// <remarks>
/// A series never reaches for the chart's size or the window's scaling by itself; everything it needs to place
/// ink is in here, which is what keeps the same series code working in a control, in a headless test and in a
/// future renderer that is not Avalonia at all.
/// </remarks>
/// <param name="transform">The transform that maps values onto the plot.</param>
/// <param name="plotBounds">The rectangle the data is mapped into.</param>
/// <param name="renderScaling">How many device pixels one logical pixel covers.</param>
/// <param name="bands">Where the band-laid series stand, when the chart has any; null when it has none.</param>
public readonly struct SeriesRenderContext(
    DataTransform transform,
    Rect plotBounds,
    double renderScaling,
    BandLayout? bands = null)
{
    /// <summary>The transform that maps values onto the plot.</summary>
    public DataTransform Transform { get; } = transform;

    /// <summary>The rectangle the data is mapped into.</summary>
    public Rect PlotBounds { get; } = plotBounds;

    /// <summary>How many device pixels one logical pixel covers.</summary>
    public double RenderScaling { get; } = renderScaling > 0 ? renderScaling : 1;

    /// <summary>
    /// Where the series that are laid out in bands - columns, bars - stand within the slots they share.
    /// </summary>
    /// <remarks>Null when the chart has no band series, which is every chart of lines and areas.</remarks>
    public BandLayout? Bands { get; } = bands;

    /// <summary>Places a value pair, or says there is no place for it.</summary>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <param name="point">The position, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when either value is missing.</returns>
    public bool TryPoint(ChartValue x, ChartValue y, out Point point) =>
        Transform.TryValueToPoint(x, y, out point);

    /// <summary>Places a pair of unit values, which is what a series maps once its data is normalized.</summary>
    /// <param name="x">The value on the horizontal dimension, in unit space.</param>
    /// <param name="y">The value on the vertical dimension, in unit space.</param>
    /// <param name="point">The position, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when there is no place for the pair.</returns>
    public bool TryUnitPoint(double x, double y, out Point point) =>
        Transform.TryUnitToPoint(x, y, out point);

    /// <summary>
    /// Rounds a position onto the device pixel grid: what a filled shape or a marker wants, so it lands on the
    /// same pixels at every scaling rather than smearing across two.
    /// </summary>
    /// <remarks>
    /// For the furniture of a chart - grid lines, axis lines, band edges, tick marks - and never for data. A
    /// series' points have to stay where the values put them; rounding them away from that is a small lie that
    /// a fixed-scale reading (an ECG strip, a trading chart) cannot afford.
    /// </remarks>
    /// <param name="value">The coordinate.</param>
    /// <returns>The coordinate, on a pixel boundary.</returns>
    public double Snap(double value) => RenderScaling > 0 ? Math.Round(value * RenderScaling) / RenderScaling : value;

    /// <summary>
    /// Places a stroke so that it fills whole device pixels: an odd number of pixels wide has to sit half a
    /// pixel off the boundary, or it is drawn across two rows of half brightness and looks blurred.
    /// </summary>
    /// <param name="value">The centre of the stroke.</param>
    /// <param name="thickness">The thickness of the stroke, in logical pixels.</param>
    /// <returns>The coordinate to draw the stroke at.</returns>
    public double SnapStroke(double value, double thickness)
    {
        var width = Math.Max(1, Math.Round(thickness * RenderScaling));
        var offset = width % 2 == 1 ? 0.5 : 0;

        return (Math.Floor(value * RenderScaling) + offset) / RenderScaling;
    }
}
