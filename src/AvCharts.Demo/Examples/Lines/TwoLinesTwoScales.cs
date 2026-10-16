using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Lines;

/// <summary>
/// The same chart as <c>TwoLinesTwoScales.axaml</c>, written in code: two readings a day, each measured against
/// a scale of its own.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. A second scale is an axis of the
/// series' own - the chart draws one scale of its own on the left and any scale a series brings on the far side -
/// which is what lets two readings of different sizes be read together without one of them being a flat line.
/// </remarks>
public sealed class TwoLinesTwoScales : IExample
{
    /// <summary>Rainfall, in millimetres: what the left-hand scale is for.</summary>
    public static IEnumerable<object> Millimetres { get; } = Readings(10, 40, 25, 70, 55, 90);

    /// <summary>And how much sun there was, in hours: what the right-hand scale is for.</summary>
    public static IEnumerable<object> Sunshine { get; } = Readings(1.2, 2.6, 3.4, 2.1, 3.8, 1.6);

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 5, Title = "Days" },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100, Title = "Millimetres" },
        };

        chart.Series.Add(
            new LineSeries<Reading>
            {
                DisplayName = "Millimetres",
                ItemsSource = Millimetres.Cast<Reading>(),
                XSelector = reading => reading.Day,
                YSelector = reading => reading.Value,
            });

        chart.Series.Add(
            new LineSeries<Reading>
            {
                DisplayName = "Sunshine",
                ItemsSource = Sunshine.Cast<Reading>(),
                XSelector = reading => reading.Day,
                YSelector = reading => reading.Value,

                // The scale of its own: the chart draws it on the side away from the scale it owns.
                VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4, Title = "Hours" },
            });

        return chart;
    }

    /// <summary>A day of readings, one a day for a week.</summary>
    /// <param name="values">What it was worth each day.</param>
    /// <returns>The readings.</returns>
    private static IEnumerable<object> Readings(params double[] values) =>
        [.. values.Select((value, day) => new Reading(day, value))];

    /// <summary>One reading: which day, and what it was worth.</summary>
    /// <param name="Day">Which day.</param>
    /// <param name="Value">What it was worth.</param>
    public sealed record Reading(double Day, double Value);
}
