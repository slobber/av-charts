using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Columns;

/// <summary>
/// The same chart as <c>AColumnWithALineOverIt.axaml</c>, written in code: columns of traffic with the rate that
/// came out of them drawn over the top.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. A combination chart is not a kind of
/// chart here: any series may be added to any chart, which is why the two halves of this one are a
/// <c>ColumnSeries</c> and a <c>LineSeries</c> in the same list. The line is drawn second, so it is over the
/// columns rather than under them.
/// </remarks>
public sealed class AColumnWithALineOverIt : IExample
{
    /// <summary>How much traffic there was.</summary>
    public static IEnumerable<object> Traffic { get; } =
    [
        new Reading("Mon", 6),
        new Reading("Tue", 3),
        new Reading("Wed", 7),
        new Reading("Thu", 4),
        new Reading("Fri", 8),
    ];

    /// <summary>And how much of it was answered.</summary>
    public static IEnumerable<object> Rates { get; } =
    [
        new Reading("Mon", 4),
        new Reading("Tue", 6),
        new Reading("Wed", 5),
        new Reading("Thu", 7),
        new Reading("Fri", 6),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 2 },
        };

        chart.Series.Add(
            new ColumnSeries<Reading>
            {
                DisplayName = "Traffic",
                ItemsSource = Traffic.Cast<Reading>(),
                XSelector = reading => reading.Name,
                YSelector = reading => reading.Value,
                ColumnWidthRatio = 0.5,
            });

        chart.Series.Add(
            new LineSeries<Reading>
            {
                DisplayName = "Success rate",
                ItemsSource = Rates.Cast<Reading>(),
                XSelector = reading => reading.Name,
                YSelector = reading => reading.Value,
                Marker = Markers.Circle,
                MarkerSize = 5,
            });

        return chart;
    }

    /// <summary>One reading: what it is called, and what it was worth.</summary>
    /// <param name="Name">What it is called.</param>
    /// <param name="Value">What it was worth.</param>
    public sealed record Reading(string Name, double Value);
}
