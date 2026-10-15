using System.Globalization;

namespace AvCharts.Themes;

/// <summary>
/// The names of the colour tokens the default theme supplies, and the names a host overrides to reskin a chart.
/// </summary>
/// <remarks>
/// <para>
/// These strings are public API: each one is a resource key the default theme fills in, the key a host
/// overrides in its own resources, and the key the chart reads when it paints. Renaming one breaks every host
/// that had overridden it, which is why they are constants here rather than literals spread through the code.
/// A host needs no new API to reskin a chart - it puts a value under the same key in the resources of the chart
/// or of anything above it.
/// </para>
/// <para>
/// A token is one of two types, and the type says what it is for. A fill is an
/// <see cref="Avalonia.Media.IBrush"/>: a surface, text, or the inside of a mark. A stroke is an
/// <see cref="Avalonia.Media.Pen"/>, which carries its own brush along with its width, dashes and caps - so a
/// dashed crosshair or a gradient series line costs a host nothing but the value.
/// </para>
/// </remarks>
public static class ChartTokens
{
    /// <summary>How many colours the series palette holds: <c>Series.Stroke1</c> to <c>Series.Stroke10</c>.</summary>
    public const int PaletteSize = 10;

    /// <summary>The brush the surface of the chart is filled with.</summary>
    public const string SurfaceBackground = "Surface.Background";

    /// <summary>The pen the edge of the surface is drawn with.</summary>
    public const string SurfaceBorder = "Surface.Border";

    /// <summary>The pen drawn around the surface while the chart holds the keyboard focus.</summary>
    public const string SurfaceFocusRing = "Surface.FocusRing";

    /// <summary>The brush of a shaded band along an axis, running one direction across the plot.</summary>
    public const string AxisBandFill = "Axis.Band.Fill";

    /// <summary>The brush of the band between it, so that alternate bands can read differently.</summary>
    public const string AxisBandFillAlt = "Axis.Band.FillAlt";

    /// <summary>The pen of the axis line itself.</summary>
    public const string AxisLine = "Axis.Line";

    /// <summary>The pen of the small marks between the axis line and its labels.</summary>
    public const string AxisTick = "Axis.Tick";

    /// <summary>The brush the tick labels are written in.</summary>
    public const string AxisLabelForeground = "Axis.Label.Foreground";

    /// <summary>The brush the axis title is written in.</summary>
    public const string AxisTitleForeground = "Axis.Title.Foreground";

    /// <summary>The pen of the grid lines at the labelled intervals.</summary>
    public const string AxisGridLineMajor = "Axis.GridLine.Major";

    /// <summary>The pen of the finer grid lines between them.</summary>
    public const string AxisGridLineMinor = "Axis.GridLine.Minor";

    /// <summary>
    /// The pen of the line where the value is zero, which an engineering drawing calls the origin line.
    /// </summary>
    /// <remarks>
    /// Separate from the other grid lines because it is not one of them: Flex draws it from its own settings
    /// (<c>horizontalShowOrigin</c>, <c>verticalShowOrigin</c>) and usually heavier than the rest, and a chart
    /// that cannot say that ends up drawing the zero line at the same weight as every other line.
    /// </remarks>
    public const string AxisGridLineOrigin = "Axis.GridLine.Origin";

    /// <summary>The brush of a marker's inside.</summary>
    /// <remarks>
    /// Kept for the marks and annotations a host adds to a chart of its own - a flagged reading, a note, a
    /// reference point - which is what the token is for. The library's own series paint their markers in the
    /// series' colours, because a marker says which series a reading belongs to, and a theme does not get to
    /// overrule that; the hover and selected rings below are painted with their own tokens by the selection
    /// layer, which is the chart's furniture rather than a series'.
    /// </remarks>
    public const string MarkerFill = "Marker.Fill";

    /// <summary>The pen of a marker's edge; the same story as <see cref="MarkerFill"/>.</summary>
    public const string MarkerStroke = "Marker.Stroke";

    /// <summary>The brush of a marker's inside while the pointer is over it.</summary>
    public const string MarkerHoverFill = "Marker.Hover.Fill";

    /// <summary>The pen of a marker's edge while the pointer is over it.</summary>
    public const string MarkerHoverStroke = "Marker.Hover.Stroke";

    /// <summary>The brush of a marker's inside while it is selected.</summary>
    public const string MarkerSelectedFill = "Marker.Selected.Fill";

    /// <summary>The pen of a marker's edge while it is selected.</summary>
    public const string MarkerSelectedStroke = "Marker.Selected.Stroke";

    /// <summary>The pen the crosshair lines are drawn with.</summary>
    public const string CrosshairLine = "Crosshair.Line";

    /// <summary>The brush of the chip that carries the crosshair's readout.</summary>
    public const string CrosshairLabelBackground = "Crosshair.Label.Background";

    /// <summary>The brush the crosshair's readout is written in.</summary>
    public const string CrosshairLabelForeground = "Crosshair.Label.Foreground";

    /// <summary>The brush behind a legend.</summary>
    public const string LegendBackground = "Legend.Background";

    /// <summary>The pen around a legend.</summary>
    public const string LegendBorder = "Legend.Border";

    /// <summary>The brush a legend's text is written in.</summary>
    public const string LegendForeground = "Legend.Foreground";

    /// <summary>The brush of the entry of a series that is hidden.</summary>
    public const string LegendItemDisabled = "Legend.Item.Disabled";

    /// <summary>The brush behind a data tip.</summary>
    public const string DataTipBackground = "DataTip.Background";

    /// <summary>The pen around a data tip.</summary>
    public const string DataTipBorder = "DataTip.Border";

    /// <summary>The brush a data tip's text is written in.</summary>
    public const string DataTipForeground = "DataTip.Foreground";

    /// <summary>The brush a data tip casts below itself.</summary>
    public const string DataTipShadow = "DataTip.Shadow";

    /// <summary>The brush of a mark that stands for a rising value.</summary>
    public const string FinancialUp = "Financial.Up";

    /// <summary>The brush of a mark that stands for a falling value.</summary>
    public const string FinancialDown = "Financial.Down";

    /// <summary>The brush inside the rectangle dragged over a region to select it.</summary>
    public const string SelectionRubberBandFill = "Selection.RubberBand.Fill";

    /// <summary>The pen around the rectangle dragged over a region to select it.</summary>
    public const string SelectionRubberBandStroke = "Selection.RubberBand.Stroke";

    /// <summary>The pen of the stroke of the series at a slot, counting from one.</summary>
    /// <param name="slot">The slot in the palette, from 1 to <see cref="PaletteSize"/>.</param>
    /// <returns>The resource key.</returns>
    public static string SeriesStroke(int slot) => "Series.Stroke" + slot.ToString(CultureInfo.InvariantCulture);

    /// <summary>The brush of the fill of the series at a slot, counting from one.</summary>
    /// <param name="slot">The slot in the palette, from 1 to <see cref="PaletteSize"/>.</param>
    /// <returns>The resource key.</returns>
    public static string SeriesFill(int slot) => "Series.Fill" + slot.ToString(CultureInfo.InvariantCulture);
}
