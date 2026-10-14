using Avalonia.Controls;
using Avalonia.Layout;
using AvCharts.Controls;

// The chart's own SelectionMode has the same name as Avalonia's, so a host that imports both says which: one
// alias here, and the rest of the page reads the way it would in an application.
using ChartSelection = AvCharts.Controls.SelectionMode;
namespace AvCharts.Demo;

// The page of tips, a legend and a crosshair.
public static partial class Pages
{
    /// <summary>Tips, a legend, and a crosshair: what a reader gets when they point at the chart.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Interaction()
    {
        var page = new DockPanel();
        var chart = Chart(0, 6, 0, 10);

        chart.Series.Add(Line("Depth", [2.0, 5.0, 3.0, 7.0, 6.0, 9.0]));
        chart.Series.Add(Line("Speed", [1.0, 2.0, 1.5, 3.0, 2.5, 4.0]));
        chart.ShowCrosshair = true;
        chart.SelectionMode = ChartSelection.Single;

        // Every reading is written out, because a page is drawn once and there is no pointer to hover with: in the
        // window a reader points at a chart and gets one tip at a time.
        chart.ShowAllDataTips = true;

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(legend, Dock.Right);

        page.Children.Add(legend);
        page.Children.Add(chart);
        return Page(
            "Interaction",
            "What a reader gets when they point at a chart.",
            Example("A tip per reading, a legend, and a crosshair", page),
            Try("Try it: the tips, the crosshair, the legend and the selection", InteractionPlayground()));
    }

    /// <summary>
    /// The same chart with the four ways of answering a pointer on knobs.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// Every one of these is a setting a host turns on for its own chart, so they are worth a switch each: what a
    /// tip is written for, whether a crosshair follows the pointer, whether a click selects a reading or a run of
    /// them, and whether the legend is there to say which line is which.
    /// </remarks>
    private static Control InteractionPlayground()
    {
        var chart = Chart(0, 6, 0, 10);

        chart.Series.Add(Line("Depth", [2.0, 5.0, 3.0, 7.0, 6.0, 9.0]));
        chart.Series.Add(Line("Speed", [1.0, 2.0, 1.5, 3.0, 2.5, 4.0]));
        chart.ShowCrosshair = true;
        chart.SelectionMode = ChartSelection.Single;

        void Tips(string picked)
        {
            chart.ShowAllDataTips = picked == "every reading";
            chart.DataTipMode = picked switch
            {
                "every reading" => DataTipMode.Item,
                "the pointer" => DataTipMode.Mouse,
                _ => DataTipMode.None,
            };
        }

        Tips("every reading");

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Right);
        page.Children.Add(legend);
        page.Children.Add(chart);

        var selections = new[] { ChartSelection.Single, ChartSelection.Multiple, ChartSelection.None };

        return Playground(
            page,
            Choice("Tips", ["every reading", "the pointer", "none"], "every reading", Tips),
            OnOff("Crosshair", true, on => chart.ShowCrosshair = on),
            Choices("Selection", ["one", "many", "off"], 0, picked => chart.SelectionMode = selections[picked]),
            OnOff("Legend", true, on => legend.IsVisible = on));
    }
}
