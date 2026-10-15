using AvCharts.Charts;

namespace AvCharts.Controls;

/// <summary>
/// A chart of shares: a circle whose wedges are what its series is made of.
/// </summary>
/// <remarks>
/// <para>
/// The second chart this library draws with, and the small one: a Cartesian chart lays out two axes, measures
/// their gutters and hands the result to a transform, while a pie has no axes to lay out at all - so what is
/// left is the wiring, which is the transform it is drawn in.
/// </para>
/// <para>
/// Which is the extension contract in the design brief paying for itself: the layers, the hit testing, the data
/// tips and the legend are the same ones every other chart uses, and the coordinate system is the one thing
/// that is different.
/// </para>
/// </remarks>
public class PieChart : ChartBase
{
    /// <summary>Makes a chart of shares, drawn in a circle.</summary>
    public PieChart()
    {
        // No axes are added, and that is the whole of the difference: a Cartesian chart adds two and measures
        // what their labels need, while a pie's picture is a circle in the room it was given.
        Transform = new PolarTransform();
    }
}
