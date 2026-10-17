using System.Globalization;
using AvCharts.Controls;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The table page: the same series data as a read-only grid, with no axes and no plot.
internal static class SeriesTablePage
{
    /// <summary>Three live-looking channels as columns, and the same three as rows.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var table = new SeriesTable
        {
            Height = 260,
            CellFormatter = Rounded,
        };

        table.Series.Add(Channel("Temperature", index => 21 + (3 * Math.Sin(index / 4.0))));
        table.Series.Add(Channel("Pressure", index => 101.2 + (0.8 * Math.Cos(index / 3.0))));
        table.Series.Add(Channel("Flow", index => 14 + (2 * Math.Sin((index + 2) / 5.0))));

        var transposed = new SeriesTable
        {
            Height = 160,
            Orientation = SeriesTableOrientation.SeriesAsRows,
            CellFormatter = Rounded,
        };

        transposed.Series.Add(Channel("Temperature", index => 21 + (3 * Math.Sin(index / 4.0)), 8));
        transposed.Series.Add(Channel("Pressure", index => 101.2 + (0.8 * Math.Cos(index / 3.0)), 8));
        transposed.Series.Add(Channel("Flow", index => 14 + (2 * Math.Sin((index + 2) / 5.0)), 8));

        return Page(
            "Table",
            "Series as a read-only table: no axes, no plot, and a row for every record.",
            Example("Three channels, one column each", table),
            Example("The same three channels as rows", transposed));
    }

    private static LineSeries<double> Channel(string name, Func<int, double> value, int count = 48) =>
        new()
        {
            DisplayName = name,
            ItemsSource = [.. Enumerable.Range(0, count).Select(value)],
            YSelector = reading => reading,
        };

    private static string Rounded(SeriesBase series, int index) =>
        double.TryParse(series.ValueTextAt(index), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("0.00", CultureInfo.InvariantCulture)
            : series.ValueTextAt(index);
}
