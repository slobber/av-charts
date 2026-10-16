using Avalonia.Controls;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Bars page: the same kind of thing lying down, against categories - and the playground beside it.
internal static class BarsPage
{
    /// <summary>Bars, which are columns lying down, against category names.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 8 },
            VerticalAxis = new CategoryAxis(),
        };

        TwoYears(chart);

        return Page(
            "Bars",
            "Columns lying down, against the names of things.",
            Sourced("Two years of bars against the names of things", "Bars/TwoSeriesAgainstCategories"),
            Try("Try it: the same bars, arranged the way you ask", BarPlayground()));
    }

    /// <summary>
    /// The bars with knobs on: the same questions as the columns, lying down.
    /// </summary>
    /// <returns>The playground.</returns>
    private static Control BarPlayground()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 8 },
            VerticalAxis = new CategoryAxis(),
        };

        TwoYears(chart);

        // Overlaid first, which is what the page draws and what a bar chart with two series wants before anyone
        // has said otherwise: the two years stand in the same slot until the reader asks for them side by side.
        var arrangements = new[] { SeriesLayout.Overlay, SeriesLayout.Cluster, SeriesLayout.Stacked };
        var names = new[] { "over each other", "side by side", "stacked" };
        var widths = new[] { 0.4, 0.65, 0.9 };

        void Arranged(string picked) => chart.SeriesLayout = arrangements[Array.IndexOf(names, picked)];

        void Width(double ratio)
        {
            foreach (var series in chart.Series.OfType<ColumnSeries<Reading>>())
            {
                series.ColumnWidthRatio = ratio;
            }
        }

        return Playground(
            chart,
            Choice("Arrangement", names, names[0], Arranged),
            Choices("Bar width", ["40%", "65%", "90%"], 1, picked => Width(widths[picked])));
    }
}
