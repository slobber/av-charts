using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;

namespace AvCharts.Demo.Examples.Candles;

/// <summary>
/// The same chart as <c>HighLowCloseBars.axaml</c>, written in code: a week of prices as high-low-close bars.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. The readings are a view model's, the
/// way an application's would be - static, and written as a sequence of objects, because a series is a model
/// object rather than a control and a page cannot bind one into another.
/// </remarks>
public sealed class HighLowCloseBars : IExample
{
    /// <summary>The prices: the day, and the four prices that day was worth.</summary>
    /// <remarks>
    /// A price a session rather than four unrelated readings: each day opens where the one before it closed,
    /// which is what makes a week of these one price that moved.
    /// </remarks>
    public static IEnumerable<object> Prices { get; } =
    [
        new Session(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), 104.20, 106.10, 103.40, 105.60),
        new Session(new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero), 105.60, 106.40, 104.10, 104.50),
        new Session(new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero), 104.50, 105.20, 102.90, 103.20),
        new Session(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero), 103.20, 104.80, 102.40, 104.60),
        new Session(new DateTimeOffset(2026, 3, 6, 0, 0, 0, TimeSpan.Zero), 104.60, 106.90, 104.30, 106.40),
        new Session(new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.Zero), 106.40, 107.20, 104.80, 105.10),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            // Real dates, so that the axis reads as a calendar and the bars stand a session apart.
            Height = 180,
            HorizontalAxis = new DateTimeAxis(),
            VerticalAxis = new LinearAxis(),
        };

        chart.Series.Add(
            new HighLowCloseSeries<Session>
            {
                DisplayName = "Northwind",
                ItemsSource = Prices.Cast<Session>(),
                XSelector = session => session.Day,
                OpenSelector = session => session.Open,
                HighSelector = session => session.High,
                LowSelector = session => session.Low,
                CloseSelector = session => session.Close,

                // A tick reaches half a band from the middle, so how much of the slot a bar takes is how long its
                // ticks are - and a bar of prices wants ticks short enough to read the two prices and not so
                // short that the bar looks like a plain line.
                BandWidthRatio = 0.35,
            });

        return chart;
    }

    /// <summary>One session: the day it was, and the four prices it was worth.</summary>
    /// <param name="Day">The day.</param>
    /// <param name="Open">Where it opened.</param>
    /// <param name="High">The highest it reached.</param>
    /// <param name="Low">The lowest it reached.</param>
    /// <param name="Close">Where it closed.</param>
    public sealed record Session(DateTimeOffset Day, double Open, double High, double Low, double Close);
}
