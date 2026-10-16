using Avalonia.Controls;
using Avalonia.Styling;

using AvCharts.Series;




using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Columns page: columns clustered, stacked, and with a line over them - and the playground beside them.
internal static class ColumnsPage
{
    /// <summary>Columns three ways: side by side, stacked, and with a line over them.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var one = new[] { 3.0, 5.0, 2.0, 6.0, 4.0, 7.0 };
        var other = new[] { 2.0, 1.0, 4.0, 2.0, 3.0, 1.0 };

        // The window is the readings' own bounds rather than a neat nought to six: a column stands in a slot that
        // reaches half a slot past each of its readings, so six readings of a column series occupy minus a half to
        // five and a half - and a window of nought to six would cut the first column and draw it in the axis'
        // gutter, which is what this page used to do.
        var clustered = Chart(-0.5, 5.5, 0, 9);
        clustered.SeriesLayout = SeriesLayout.Cluster;
        clustered.Series.Add(Column("This year", one));
        clustered.Series.Add(Column("Last year", other));

        var stacked = Chart(-0.5, 5.5, 0, 9);
        stacked.SeriesLayout = SeriesLayout.Stacked;
        stacked.Series.Add(Column("This year", one));
        stacked.Series.Add(Column("Last year", other));

        var withALine = Chart(-0.5, 5.5, 0, 9);
        withALine.Series.Add(Column("Readings", one));
        withALine.Series.Add(Line("Average", [3.0, 4.0, 3.0, 4.0, 3.5, 4.0]));

        page.Children.Add(Cell(clustered, 0, 0, "Clustered"));
        page.Children.Add(Cell(stacked, 1, 0, "Stacked"));
        page.Children.Add(Cell(withALine, 2, 0, "Columns with a line"));
        return Page(
            "Columns",
            "Columns three ways: side by side, stacked, and with a line over them.",
            Sourced("Two years stacked in one column a region", "Columns/TwoYearsStacked"),
            Sourced("Columns of traffic with the rate drawn over them", "Columns/AColumnWithALineOverIt"),
            Try("Try it: two years of columns, arranged the way you ask", ColumnPlayground()));
    }

    /// <summary>
    /// The columns with knobs on: how the series share a slot, and how much of it a column takes.
    /// </summary>
    /// <returns>The playground.</returns>
    private static Control ColumnPlayground()
    {
        var one = new[] { 3.0, 5.0, 2.0, 6.0, 4.0, 7.0 };
        var other = new[] { 2.0, 1.0, 4.0, 2.0, 3.0, 1.0 };

        // The same window as the page and for the same reason: a column stands in a slot reaching half a slot
        // past its reading, so the window of six readings is minus a half to five and a half rather than nought
        // to six. Get that wrong and the first and last columns are cut and drawn in the axis' gutter.
        var chart = Chart(-0.5, 5.5, 0, 9);

        chart.Series.Add(Column("This year", one));
        chart.Series.Add(Column("Last year", other));

        var arrangements = new[] { SeriesLayout.Cluster, SeriesLayout.Stacked, SeriesLayout.Overlay };
        var names = new[] { "side by side", "stacked", "over each other" };
        var widths = new[] { 0.4, 0.65, 0.9 };

        // Picked rather than pressed: three names this long are wider than the column the knobs stand in, and a
        // group of buttons that runs off the side is a choice nobody can read.
        void Arranged(string picked) => chart.SeriesLayout = arrangements[Array.IndexOf(names, picked)];

        void Width(double ratio)
        {
            // Through the concrete series rather than through IColumn: the interface is what a band layout reads,
            // and a width is a setting only a column has.
            foreach (var series in chart.Series.OfType<ColumnSeries<double>>())
            {
                series.ColumnWidthRatio = ratio;
            }
        }

        return Playground(
            chart,
            Choice("Arrangement", names, names[0], Arranged),
            Choices("Column width", ["40%", "65%", "90%"], 1, picked => Width(widths[picked])));
    }
}
