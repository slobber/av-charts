using System.Globalization;
using Avalonia;
using Avalonia.Media;
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
            CellStyleSelector = Signed,
        };

        table.Series.Add(Channel("Temperature", index => 21 + (3 * Math.Sin(index / 4.0))));
        table.Series.Add(Channel("Pressure", index => 101.2 + (0.8 * Math.Cos(index / 3.0))));
        table.Series.Add(Channel("Flow", index => 14 + (2 * Math.Sin((index + 2) / 5.0))));
        table.Series.Add(Channel("Balance", index => 9 * Math.Sin(index / 2.5)));

        var transposed = new SeriesTable
        {
            Height = 160,
            Orientation = SeriesTableOrientation.SeriesAsRows,
            CellFormatter = Rounded,
            CellStyleSelector = Signed,
        };

        transposed.Series.Add(Channel("Temperature", index => 21 + (3 * Math.Sin(index / 4.0)), 8));
        transposed.Series.Add(Channel("Pressure", index => 101.2 + (0.8 * Math.Cos(index / 3.0)), 8));
        transposed.Series.Add(Channel("Flow", index => 14 + (2 * Math.Sin((index + 2) / 5.0)), 8));
        transposed.Series.Add(Channel("Balance", index => 9 * Math.Sin(index / 2.5), 8));

        return Page(
            "Table",
            "Series as a read-only table: no axes, no plot, and a row for every record.",
            Example("Three channels and a signed balance, one column each", table),
            Example("The same channels as rows", transposed));
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

    private static SeriesTableCellStyle? Signed(SeriesBase series, int index)
    {
        if (series.DisplayName != "Balance" ||
            !double.TryParse(series.ValueTextAt(index), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return value < 0
            ? new SeriesTableCellStyle(
                Background: Brush.Parse("#FFF1F0"),
                Foreground: Brush.Parse("#B42318"),
                BorderBrush: Brush.Parse("#D92D20"),
                BorderThickness: new Thickness(1))
            : new SeriesTableCellStyle(
                Background: Brush.Parse("#ECFDF3"),
                Foreground: Brush.Parse("#067647"),
                BorderBrush: Brush.Parse("#12B76A"),
                BorderThickness: new Thickness(1));
    }
}
