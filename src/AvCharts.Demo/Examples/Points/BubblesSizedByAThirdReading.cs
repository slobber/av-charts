using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Points;

/// <summary>
/// The same chart as <c>BubblesSizedByAThirdReading.axaml</c>, written in code: a reading placed by two numbers
/// and sized by a third.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. A mark is a reading at a place; a
/// bubble is the same reading with a size, which is the third number a chart of places is usually drawn from -
/// how many, how much, how big - and the bubble is what reads it.
/// </remarks>
public sealed class BubblesSizedByAThirdReading : IExample
{
    /// <summary>The places: where each stands, and how big it is.</summary>
    public static IEnumerable<object> Sites { get; } =
    [
        new Site(2, 7, 6),
        new Site(4, 3, 12),
        new Site(6, 8, 4),
        new Site(8, 5, 9),
        new Site(3, 2, 3),
        new Site(7, 9, 7),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
        };

        chart.Series.Add(
            new BubbleSeries<Site>
            {
                DisplayName = "Sites",
                ItemsSource = Sites.Cast<Site>(),
                XSelector = site => site.Across,
                YSelector = site => site.Up,
                RadiusSelector = site => site.Size,
            });

        return chart;
    }

    /// <summary>One place: where it stands, and how big it is.</summary>
    /// <param name="Across">How far across it stands.</param>
    /// <param name="Up">How far up it stands.</param>
    /// <param name="Size">How big the bubble it wears is, in pixels.</param>
    public sealed record Site(double Across, double Up, double Size);
}
