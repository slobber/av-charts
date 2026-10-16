using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;

namespace AvCharts.Demo.Examples.Bars;

/// <summary>
/// The same chart as <c>TwoSeriesAgainstCategories.axaml</c>, written in code: two years of readings as bars
/// against the names of things.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. A bar is a column lying down, and
/// that is the whole of the difference: the numbers are read off the axis along the bottom and the names stand
/// up the side, so the category axis is the vertical one and the two series share the row they are in.
/// </remarks>
public sealed class TwoSeriesAgainstCategories : IExample
{
    /// <summary>What was sold this year, by region.</summary>
    public static IEnumerable<object> ThisYear { get; } =
    [
        new Reading("North", 6),
        new Reading("South", 3),
        new Reading("East", 7),
        new Reading("West", 4),
        new Reading("Middle", 5),
    ];

    /// <summary>And the year before it.</summary>
    public static IEnumerable<object> LastYear { get; } =
    [
        new Reading("North", 3),
        new Reading("South", 2),
        new Reading("East", 4),
        new Reading("West", 2),
        new Reading("Middle", 3),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 8, Interval = 2 },
            VerticalAxis = new CategoryAxis(),
        };

        chart.Series.Add(Series(ThisYear, "This year"));
        chart.Series.Add(Series(LastYear, "Last year"));

        return chart;
    }

    /// <summary>One year's readings, as a series of bars.</summary>
    /// <param name="readings">The readings.</param>
    /// <param name="name">What the series is called.</param>
    /// <returns>The series.</returns>
    private static BarSeries<Reading> Series(IEnumerable<object> readings, string name) =>
        new()
        {
            DisplayName = name,
            ItemsSource = readings.Cast<Reading>(),
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Name,
        };

    /// <summary>One reading: what a region is called, and what it was worth.</summary>
    /// <param name="Name">What the region is called.</param>
    /// <param name="Value">What it was worth.</param>
    public sealed record Reading(string Name, double Value);
}
