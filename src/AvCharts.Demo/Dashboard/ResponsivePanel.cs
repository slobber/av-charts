using Avalonia;
using Avalonia.Controls;

namespace AvCharts.Demo.Dashboard;

/// <summary>Arranges its children in two columns above a width, and in one below it.</summary>
/// <remarks>
/// The dashboard's pairs are charts and tables rather than cards of equal height, so this measures each
/// child with the width it will actually get and lets a row be as tall as its taller child.
/// </remarks>
public sealed class ResponsivePanel : Panel
{
    /// <summary>The width at which the panel switches to two columns.</summary>
    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<ResponsivePanel, double>(nameof(Breakpoint), 1000);

    /// <summary>The gap between columns and rows.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ResponsivePanel, double>(nameof(Spacing), 12);

    /// <summary>The width at which the panel switches to two columns.</summary>
    public double Breakpoint
    {
        get => GetValue(BreakpointProperty);
        set => SetValue(BreakpointProperty, value);
    }

    /// <summary>The gap between columns and rows.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = double.IsFinite(availableSize.Width) && availableSize.Width >= Breakpoint ? 2 : 1;
        var width = columns == 1
            ? availableSize.Width
            : Math.Max(0, (availableSize.Width - Spacing) / 2);
        var height = 0d;
        var row = 0d;

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            child.Measure(new Size(width, availableSize.Height));
            row = Math.Max(row, child.DesiredSize.Height);

            if ((index + 1) % columns == 0 || index == Children.Count - 1)
            {
                height += row;

                if (index != Children.Count - 1)
                {
                    height += Spacing;
                }

                row = 0;
            }
        }

        return new Size(availableSize.Width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = finalSize.Width >= Breakpoint ? 2 : 1;
        var width = columns == 1
            ? finalSize.Width
            : Math.Max(0, (finalSize.Width - Spacing) / 2);
        var y = 0d;

        for (var index = 0; index < Children.Count; index += columns)
        {
            var row = 0d;

            for (var column = 0; column < columns && index + column < Children.Count; column++)
            {
                var child = Children[index + column];
                var x = columns == 1 ? 0 : column * (width + Spacing);

                child.Arrange(new Rect(x, y, width, child.DesiredSize.Height));
                row = Math.Max(row, child.DesiredSize.Height);
            }

            y += row + Spacing;
        }

        return finalSize;
    }
}
