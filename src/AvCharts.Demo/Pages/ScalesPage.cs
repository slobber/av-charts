using Avalonia.Controls;
using Avalonia.Layout;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The page of two vertical scales on one plot.
internal static class ScalesPage
{
    /// <summary>Two vertical scales on one plot, with a legend that says which series is which.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var page = new DockPanel();
        var chart = Chart(0, 6, 0, 100);

        // The axes are named, so that which scale is which reads off the chart itself and not only off the legend:
        // the horizontal one below the plot, and the two vertical ones beside it, read upwards.
        chart.HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 6, Title = "Days" };
        chart.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100, Title = "Millimetres" };

        // The millimetres are read on the chart's own scale, which is the one on the left; only the inches need a
        // second scale, and a scale a series brings is drawn on the far side.
        chart.Series.Add(Line("Millimetres", [10, 40, 25, 70, 55, 90]));

        var inches = Line("Inches", [1.0, 2.0, 1.5, 3.0, 2.5, 3.5]);

        inches.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4, Title = "Inches" };
        chart.Series.Add(inches);
        var legend = new Legend
        {
            Chart = chart,
            Placement = LegendPlacement.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

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

        // A legend on one side of the plot, saying which side it is on: the lines run down it beside the plot and
        // across it above and below, and the room between the two is left on the side that faces the chart.
        var legend = new Legend
        {
            Chart = chart,
            Placement = LegendPlacement.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Right);
        page.Children.Add(legend);
        page.Children.Add(chart);

        void Place(string picked)
        {
            legend.IsVisible = picked != "hidden";

            var side = picked switch
            {
                "above the plot" => LegendPlacement.Top,
                "under the plot" => LegendPlacement.Bottom,
                "on the left" => LegendPlacement.Left,
                _ => LegendPlacement.Right,
            };

            legend.Placement = side;
            DockPanel.SetDock(
                legend,
                side switch
                {
                    LegendPlacement.Top => Dock.Top,
                    LegendPlacement.Bottom => Dock.Bottom,
                    LegendPlacement.Left => Dock.Left,
                    _ => Dock.Right,
                });

            // Docking is an attached property the panel reads when it lays its children out, and changing it does
            // not ask the panel for a pass of its own - so a legend moved from the right to underneath the plot
            // stays where it was until something else asks for one. The legend's own arrange has to be let go of
            // as well: a panel arranges a child that still has a valid arrangement where it already is.
            legend.InvalidateArrange();
            page.InvalidateMeasure();
        }

        return Playground(
            page,
            Choice("Right-hand scale", ["inches", "millimetres"], "inches", Right),
            Choice(
                "Legend",
                ["on the right", "under the plot", "above the plot", "on the left", "hidden"],
                "on the right",
                Place));
    }
}
