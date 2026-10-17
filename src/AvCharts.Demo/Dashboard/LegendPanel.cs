using Avalonia;
using Avalonia.Controls;
using AvCharts.Controls;

namespace AvCharts.Demo.Dashboard;

/// <summary>Puts a legend above a chart in a narrow page, and beside it in a wide one.</summary>
/// <remarks>
/// The legend's own placement decides whether its items run across or down; the panel decides where the block
/// goes. Both have to change together at the breakpoint, and both are decided here during layout rather than in a
/// size-changed handler, so the first frame is already in the arrangement the width asks for.
/// </remarks>
public sealed class LegendPanel : Panel
{
    /// <summary>The width at which the legend moves from above the chart to beside it.</summary>
    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<LegendPanel, double>(nameof(Breakpoint), 1000);

    /// <summary>The room between the legend and the chart.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<LegendPanel, double>(nameof(Spacing), 6);

    /// <summary>The narrowest a legend beside a chart is allowed to be.</summary>
    public static readonly StyledProperty<double> MinimumLegendWidthProperty =
        AvaloniaProperty.Register<LegendPanel, double>(nameof(MinimumLegendWidth), 96);

    private double _legendWidth;

    /// <summary>The width at which the legend moves from above the chart to beside it.</summary>
    public double Breakpoint
    {
        get => GetValue(BreakpointProperty);
        set => SetValue(BreakpointProperty, value);
    }

    /// <summary>The room between the legend and the chart.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>The narrowest a legend beside a chart is allowed to be.</summary>
    public double MinimumLegendWidth
    {
        get => GetValue(MinimumLegendWidthProperty);
        set => SetValue(MinimumLegendWidthProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }

        var legend = Children[0];
        var chart = Children[1];
        var wide = double.IsFinite(availableSize.Width) && availableSize.Width >= Breakpoint;

        if (legend is Legend item)
        {
            item.Placement = wide ? LegendPlacement.Right : LegendPlacement.Top;
        }

        if (wide)
        {
            legend.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            _legendWidth = Math.Max(MinimumLegendWidth, legend.DesiredSize.Width);
            chart.Measure(new Size(Math.Max(0, availableSize.Width - _legendWidth - Spacing), availableSize.Height));

            return new Size(availableSize.Width, Math.Max(legend.DesiredSize.Height, chart.DesiredSize.Height));
        }

        legend.Measure(new Size(availableSize.Width, availableSize.Height));
        chart.Measure(new Size(availableSize.Width, availableSize.Height));

        return new Size(availableSize.Width, legend.DesiredSize.Height + Spacing + chart.DesiredSize.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }

        var legend = Children[0];
        var chart = Children[1];

        if (finalSize.Width >= Breakpoint)
        {
            var width = Math.Max(0, finalSize.Width - _legendWidth - Spacing);

            chart.Arrange(new Rect(0, 0, width, chart.DesiredSize.Height));
            legend.Arrange(new Rect(width + Spacing, 0, _legendWidth, legend.DesiredSize.Height));
        }
        else
        {
            legend.Arrange(new Rect(0, 0, finalSize.Width, legend.DesiredSize.Height));
            chart.Arrange(new Rect(
                0,
                legend.DesiredSize.Height + Spacing,
                finalSize.Width,
                chart.DesiredSize.Height));
        }

        return finalSize;
    }
}
