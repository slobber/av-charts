using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvCharts.Controls;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// Two live tables: one where rows arrive at the top, and one where the rows stay and the cells change.
internal static class LiveTablePage
{
    /// <summary>A newest-first feed, and a fixed set of rows updated in place.</summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// Both tables are ordinary <see cref="SeriesTable"/>s. The difference is the source: the first inserts a
    /// record at the front of every series and drops the last, while the second writes over the records it already
    /// has. A table is a view of a series, so neither case needs a second table implementation.
    /// </remarks>
    public static DemoPage Create() => Page(
        "Live table",
        "Two live views: rows that arrive newest first, and rows that stay put while their cells change.",
        Example("Newest first: each reading is inserted at the top", NewestFirst()),
        Example("In place: the same rows, updated by the latest reading", InPlace()));

    private static SeriesTable NewestFirst()
    {
        var table = NewTable();
        var channels = new (string Name, Func<double, double> Value)[]
        {
            ("Time (s)", seconds => seconds),
            ("Temperature", seconds => 21 + (2.4 * Math.Sin(seconds * 0.8))),
            ("Pressure", seconds => 101.2 + (0.6 * Math.Cos(seconds * 0.55))),
            ("Balance", seconds => 9 * Math.Sin(seconds * 0.7)),
        };
        var sources = channels.Select(_ => new List<double>()).ToArray();
        var series = AddSeries(table, channels.Select(channel => channel.Name).ToArray(), sources);

        void Insert(double seconds)
        {
            for (var column = 0; column < channels.Length; column++)
            {
                sources[column].Insert(0, channels[column].Value(seconds));

                if (sources[column].Count > Rows)
                {
                    sources[column].RemoveAt(Rows);
                }

                series[column].InvalidateData();
            }
        }

        // Oldest first, each inserted at the front: the picture opens with the newest row at the top.
        for (var step = 1; step <= Rows; step++)
        {
            Insert(step * Step.TotalSeconds);
        }

        var since = Stopwatch.StartNew();
        var latest = Rows * Step.TotalSeconds;
        var clock = new DispatcherTimer { Interval = Step };

        clock.Tick += (_, _) =>
        {
            latest += since.Elapsed.TotalSeconds;
            since.Restart();
            Insert(latest);
        };

        WhileShown(table, clock.Start, clock.Stop);
        return table;
    }

    private static SeriesTable InPlace()
    {
        var table = NewTable(200);
        var channels = new (string Name, Func<double, int, double> Value)[]
        {
            ("Asset", (_, device) => device + 1),
            ("Temperature", (seconds, device) => 21 + (2.4 * Math.Sin((seconds * 0.8) + (device * 0.7)))),
            ("Pressure", (seconds, device) => 101.2 + (0.6 * Math.Cos((seconds * 0.55) + (device * 0.4)))),
            ("Balance", (seconds, device) => 9 * Math.Sin((seconds * 0.7) + (device * 0.9))),
        };
        var sources = channels
            .Select(_ => Enumerable.Repeat(0.0, Devices).ToList())
            .ToArray();
        var series = AddSeries(table, channels.Select(channel => channel.Name).ToArray(), sources);

        void Update(double seconds)
        {
            for (var device = 0; device < Devices; device++)
            {
                for (var column = 0; column < channels.Length; column++)
                {
                    sources[column][device] = channels[column].Value(seconds, device);
                }
            }

            foreach (var column in series)
            {
                column.InvalidateData();
            }
        }

        Update(0);

        var since = Stopwatch.StartNew();
        var clock = new DispatcherTimer { Interval = Step };

        clock.Tick += (_, _) => Update(since.Elapsed.TotalSeconds);

        WhileShown(table, clock.Start, clock.Stop);
        return table;
    }

    private static SeriesTable NewTable(double height = 280) => new()
    {
        Height = height,
        AutoScrollToEnd = false,
        CellFormatter = Rounded,
        CellStyleSelector = Signed,
        RowBorderSelector = RowRule,
        ColumnBorderSelector = ColumnRule,
    };

    private static LineSeries<double>[] AddSeries(
        SeriesTable table,
        IReadOnlyList<string> names,
        IReadOnlyList<List<double>> sources)
    {
        var series = names.Select((name, column) => new LineSeries<double>
        {
            DisplayName = name,
            ItemsSource = sources[column],
            YSelector = reading => reading,
        }).ToArray();

        foreach (var column in series)
        {
            table.Series.Add(column);
        }

        return series;
    }

    private static string Rounded(SeriesBase series, int index)
    {
        if (!double.TryParse(series.ValueTextAt(index), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return series.ValueTextAt(index);
        }

        if (series.DisplayName == "Asset")
        {
            return $"Line {value:0}";
        }

        return series.DisplayName == "Time (s)"
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : value.ToString("0.00", CultureInfo.InvariantCulture);
    }

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

    private static SeriesTableBorderStyle RowRule(int _) =>
        new(Brush.Parse("#D0D5DD"), new Thickness(0, 0, 0, 1));

    private static SeriesTableBorderStyle ColumnRule(int _) =>
        new(Brush.Parse("#D0D5DD"), new Thickness(0, 0, 1, 0));

    private const int Rows = 12;
    private const int Devices = 6;
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(250);
}
