using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;

namespace AvCharts.Demo.Examples.Lines;

/// <summary>
/// The same chart as <c>AGapInTheLine.axaml</c>, written in code: one week of readings with Wednesday missing.
/// </summary>
/// <remarks>
/// A missing reading is not a zero and not a guess: the library draws it as a break in the line rather than
/// inventing a reading between the two either side of it, and <c>NullValueMode</c> is what says so - a series that
/// wants the line carried across asks for <c>Connect</c> instead. A mark on every reading is what makes the missing
/// one visible: the line stops, and so do they.
/// </remarks>
public sealed class AGapInTheLine : IExample
{
    /// <summary>One reading a day, with nothing recorded on the Wednesday.</summary>
    public static IEnumerable<object> OverTheWeek { get; } =
    [
        new Reading("Mon", 3),
        new Reading("Tue", 5),
        new Reading("Wed", null),
        new Reading("Thu", 6),
        new Reading("Fri", 4),
        new Reading("Sat", 7),
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
            new LineSeries<Reading>
            {
                DisplayName = "Recorded",
                ItemsSource = OverTheWeek.Cast<Reading>(),
                XSelector = reading => reading.Day,
                YSelector = reading => reading.Value,
                NullValueMode = NullValueMode.Gap,
                Marker = Markers.Circle,
                MarkerSize = 5,
            });

        return chart;
    }

    /// <summary>One day: what it was called, and what it was worth - or nothing at all.</summary>
    /// <param name="Day">What the day is called.</param>
    /// <param name="Value">What it was worth, or null when nothing was recorded.</param>
    public sealed record Reading(string Day, double? Value);
}
