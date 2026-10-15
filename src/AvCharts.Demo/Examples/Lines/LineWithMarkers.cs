using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Lines;

/// <summary>
/// The same chart as <c>LineWithMarkers.axaml</c>, written in code: the readings, and the series that draws them.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup - so the two halves of an example
/// cannot drift apart. The readings are a view model's, the way an application's would be.
/// </remarks>
public sealed class LineWithMarkers : IExample
{
    /// <summary>The readings: a month and what it was worth.</summary>
    /// <remarks>
    /// Static, and written as a sequence of objects, for two reasons that are both about markup: a series is a
    /// model object rather than a control, so a page cannot bind a value into one - it names one instead - and a
    /// series without a type argument takes any objects rather than a typed list. A page of a real application
    /// binds its own data context and uses the typed series; this is what an example has instead.
    /// </remarks>
    public static IEnumerable<object> North { get; } =
    [
        new Reading("Jan", 3),
        new Reading("Feb", 5),
        new Reading("Mar", 4),
        new Reading("Apr", 6),
        new Reading("May", 5),
        new Reading("Jun", 7),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 8, Interval = 2 },
        };

        chart.Series.Add(
            new LineSeries<Reading>
            {
                DisplayName = "North",
                ItemsSource = North.Cast<Reading>(),
                XSelector = reading => reading.Month,
                YSelector = reading => reading.Value,
                Marker = Markers.Circle,
                MarkerSize = 5,
            });

        return chart;
    }

    /// <summary>One reading: what it is called, and what it was worth.</summary>
    /// <param name="Month">What the month is called.</param>
    /// <param name="Value">What it was worth.</param>
    public sealed record Reading(string Month, double Value);
}
