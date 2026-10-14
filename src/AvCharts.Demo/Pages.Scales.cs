using Avalonia.Controls;
using Avalonia.Layout;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo;

// The page of two vertical scales on one plot.
public static partial class Pages
{
    /// <summary>Two vertical scales on one plot, with a legend that says which series is which.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Scales()
    {
        var page = new DockPanel();
        var chart = Chart(0, 6, 0, 100);
        var millimetres = Line("Millimetres", [10, 40, 25, 70, 55, 90]);

        millimetres.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 };
        chart.Series.Add(millimetres);

        var inches = Line("Inches", [1.0, 2.0, 1.5, 3.0, 2.5, 3.5]);

        inches.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 };
        chart.Series.Add(inches);

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(legend, Dock.Right);

        page.Children.Add(legend);
        page.Children.Add(chart);
        return Page("Two scales", "One plot, two vertical scales, and a legend that says which is which.", Example("Millimetres on the left, inches on the right", page));
    }
}
