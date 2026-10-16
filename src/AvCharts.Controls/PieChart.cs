



using AvCharts.Transforms;


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

    /// <summary>
    /// How wide the hole in the middle of the chart is, as a fraction of the circle's radius: nought by default.
    /// </summary>
    /// <remarks>
    /// The hole of the whole picture rather than of one series: a chart of one pie is a doughnut with a hole this
    /// wide, and a chart of several is a set of rings from this hole out to the rim.
    /// </remarks>
    public double InnerRadius
    {
        get;
        set
        {
            var wanted = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

            if (field.Equals(wanted))
            {
                return;
            }

            field = wanted;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Where the chart of several pies divides the circle between them, which is what makes nested pies nested
    /// rather than drawn on top of each other - Flex's <c>applySeriesSet</c>, and for the same reason.
    /// </remarks>
    protected override void PrepareLayout()
    {
        var rings = Series.Count;

        // One pie on its own keeps its own two radii, hole and all, which is how a doughnut is made: the chart
        // steps in when there is more than one ring to place, or when it has a hole to place them from.
        if (rings == 0 || (rings == 1 && InnerRadius <= 0))
        {
            return;
        }

        var hole = InnerRadius;
        var width = (1 - hole) / rings;

        for (var index = 0; index < rings; index++)
        {
            Series[index].TakeBand(hole + (index * width), hole + ((index + 1) * width));
        }
    }
}
