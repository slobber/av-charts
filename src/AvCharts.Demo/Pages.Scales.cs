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
        return Page(
            "Two scales",
            "One plot, two vertical scales, and a legend that says which is which.",
            Example("Millimetres on the left, inches on the right", page),
            Try("Try it: the same readings, either scale on either side", ScalesPlayground()));
    }

    /// <summary>
    /// The same plot with knobs on: which series is measured on the right-hand scale, and where the legend is.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// The interesting knob is the one that moves the second scale from one series to the other, because that is
    /// what a second scale is for: the charts either side of the millimetres-and-inches question, with the ranges
    /// following whatever is on the far side.
    /// </remarks>
    private static Control ScalesPlayground()
    {
        var chart = Chart(0, 6, 0, 100);
        var millimetres = Line("Millimetres", [10, 40, 25, 70, 55, 90]);
        var inches = Line("Inches", [1.0, 2.0, 1.5, 3.0, 2.5, 3.5]);

        // The second scale is one axis that whichever series is on it owns: a series of its own with a scale of
        // its own is how the library reads a second scale, and handing the axis over is how the knob works.
        var second = new LinearAxis { Minimum = 0, Maximum = 4 };

        inches.VerticalAxis = second;
        chart.Series.Add(millimetres);
        chart.Series.Add(inches);

        void Right(string picked)
        {
            var byInches = picked == "inches";

            chart.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = byInches ? 100 : 4 };
            second.Minimum = 0;
            second.Maximum = byInches ? 4 : 100;
            millimetres.VerticalAxis = byInches ? null : second;
            inches.VerticalAxis = byInches ? second : null;
        }

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Right);
        page.Children.Add(legend);
        page.Children.Add(chart);

        void Place(string picked)
        {
            legend.IsVisible = picked != "hidden";
            DockPanel.SetDock(legend, picked == "under the plot" ? Dock.Bottom : Dock.Right);
        }

        return Playground(
            page,
            Choice("Right-hand scale", ["inches", "millimetres"], "inches", Right),
            Choice("Legend", ["on the right", "under the plot", "hidden"], "on the right", Place));
    }
}
