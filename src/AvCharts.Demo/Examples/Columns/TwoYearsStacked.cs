using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Columns;

/// <summary>
/// The same chart as <c>TwoYearsStacked.axaml</c>, written in code: two years of readings, one column per region
/// with the second year standing on the first.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. How several series share a slot is
/// the chart's own arrangement rather than the series': <c>Stacked</c> here, and the same two series side by side
/// or over each other for the other two arrangements. A stack is also what the axis grows to - a column of
/// columns reaches the total, which is why the scale here goes past either year on its own.
/// </remarks>
public sealed class TwoYearsStacked : IExample
{
    /// <summary>What was sold this year, by region.</summary>
    public static IEnumerable<object> ThisYear { get; } =
    [
        new Reading("North", 6),
        new Reading("South", 3),
        new Reading("East", 7),
        new Reading("West", 4),
    ];

    /// <summary>And the year before it.</summary>
    public static IEnumerable<object> LastYear { get; } =
    [
        new Reading("North", 4),
        new Reading("South", 2),
        new Reading("East", 3),
        new Reading("West", 5),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            SeriesLayout = SeriesLayout.Stacked,
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 12, Interval = 4 },
        };

        chart.Series.Add(Series(ThisYear, "This year"));
        chart.Series.Add(Series(LastYear, "Last year"));

        return chart;
    }

    /// <summary>One region's readings, as a series of columns.</summary>
    /// <param name="readings">The readings.</param>
    /// <param name="name">What the series is called.</param>
    /// <returns>The series.</returns>
    private static ColumnSeries<Reading> Series(IEnumerable<object> readings, string name) =>
        new()
        {
            DisplayName = name,
            ItemsSource = readings.Cast<Reading>(),
            XSelector = reading => reading.Name,
            YSelector = reading => reading.Value,
        };

    /// <summary>One reading: what a region is called, and what it was worth.</summary>
    /// <param name="Name">What the region is called.</param>
    /// <param name="Value">What it was worth.</param>
    public sealed record Reading(string Name, double Value);
}
