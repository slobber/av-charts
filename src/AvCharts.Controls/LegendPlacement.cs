namespace AvCharts.Controls;

/// <summary>Which side of the plot a legend is on, which is what decides how its lines are laid out.</summary>
/// <remarks>
/// <para>
/// Above or below the plot the lines run across it and wrap when there are more than fit; beside it they run
/// down it. The legend does not move itself - where it sits in a page is the host's arrangement, as it is in
/// Flex - so this says which side the host has put it on, and the two agree about the flow of the lines and
/// about which side of the legend faces the chart.
/// </para>
/// <para>
/// The gap that keeps the legend off the chart comes from the placement as well: the theme leaves it on the side
/// that faces the plot, so a legend docked to a chart does not touch it, and a host that wants another gap sets
/// a margin of its own.
/// </para>
/// </remarks>
public enum LegendPlacement
{
    /// <summary>Above the plot, with the lines running across it.</summary>
    Top,

    /// <summary>Below the plot, with the lines running across it.</summary>
    Bottom,

    /// <summary>To the left of the plot, with the lines running down it.</summary>
    Left,

    /// <summary>To the right of the plot, with the lines running down it.</summary>
    Right,
}
