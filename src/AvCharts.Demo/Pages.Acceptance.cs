using Avalonia.Controls;
using Avalonia.Layout;
using AvCharts.Charts;
using AvCharts.Controls;

// The chart's own SelectionMode has the same name as Avalonia's, so a host that imports both says which: one
// alias here, and the rest of the page reads the way it would in an application.
using ChartSelection = AvCharts.Controls.SelectionMode;
namespace AvCharts.Demo;

// The acceptance page: the list the first version is accepted against, and its own readings.
public static partial class Pages
{
    /// <summary>
    /// Everything the first version promised, on one page: a combination chart, two scales, a time axis, stacked
    /// columns, a legend, tips, a crosshair and selection - and a hundred thousand readings beside it.
    /// </summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// This is the rehearsal rather than a demonstration: it is the list a reader accepts the library against,
    /// and the test beside it walks the same list. The ECG strip has a page of its own, because a strip chart is
    /// read on its own.
    /// </remarks>
    internal static DemoPage Acceptance()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new DateTimeAxis { Interval = 86_400_000 },
            VerticalAxis = new LinearAxis(),
            SeriesLayout = SeriesLayout.Stacked,
            ShowCrosshair = true,
            ShowAllDataTips = true,
            SelectionMode = ChartSelection.Single,
        };

        var days = Enumerable.Range(0, 10).ToArray();
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var records = days
            .Select(day => new Day(start.AddDays(day), 120 + (30 * Math.Sin(day)), 96 + (8 * Math.Cos(day))))
            .ToArray();

        chart.Series.Add(new ColumnSeries<Day>
        {
            DisplayName = "Received",
            ItemsSource = records,
            XSelector = day => day.When,
            YSelector = day => day.Received,
        });

        chart.Series.Add(new ColumnSeries<Day>
        {
            DisplayName = "Rejected",
            ItemsSource = records,
            XSelector = day => day.When,
            YSelector = day => day.Rejected,
        });

        // A scale of its own, in per cent, against the same days.
        var rate = new LineSeries<Day>
        {
            DisplayName = "Success rate",
            ItemsSource = records,
            XSelector = day => day.When,
            YSelector = day => 92 + (3 * Math.Cos((day.When - start).TotalDays)),
            StrokeThickness = 2,
        };

        rate.VerticalAxis = new LinearAxis { Minimum = 80, Maximum = 100 };
        chart.Series.Add(rate);

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Right);
        page.Children.Add(legend);
        page.Children.Add(chart);

        return Page(
            "Everything at once",
            "The acceptance list on one page: a combination chart, two scales, a time axis, stacked columns, a legend, tips, a crosshair, selection - and a hundred thousand readings.",
            Example("Columns stacked under a line, with a scale each", page),
            Example("A hundred thousand readings, sampled to the pixels there are", HundredThousand()));
    }

    /// <summary>A hundred thousand readings of a signal, sampled down rather than drawn point for point.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart HundredThousand()
    {
        var chart = Chart(0, 50_000, 0, 100);

        chart.Series.Add(new LineSeries<double>
        {
            DisplayName = "Signal",
            ItemsSource = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))],
            YSelector = value => value,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        });

        return chart;
    }

    /// <summary>A day of readings, with the moment it is about: a selector is handed the record, not its place.</summary>
    /// <param name="When">The day.</param>
    /// <param name="Received">How many arrived.</param>
    /// <param name="Rejected">How many of them were rejected.</param>
    private sealed record Day(DateTimeOffset When, double Received, double Rejected);
}
