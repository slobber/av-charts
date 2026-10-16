using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;

namespace AvCharts.Demo.Examples.Candles;

/// <summary>
/// The same chart as <c>CandleBodies.axaml</c>, written in code: a week of prices as candles.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. A candle says four prices at once -
/// where the session opened and closed, which is the body, and how far it reached either way, which is the wick -
/// and the two colours it is drawn in are the theme's own rather than the series': which way a price went is not
/// a matter of where this series stands among the others.
/// </remarks>
public sealed class CandleBodies : IExample
{
    /// <summary>The prices: the day, and the four prices that day was worth.</summary>
    public static IEnumerable<object> Prices { get; } =
    [
        new Session(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), 104.20, 106.10, 103.40, 105.60),
        new Session(new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero), 105.60, 106.40, 104.10, 104.50),
        new Session(new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero), 104.50, 105.20, 102.90, 103.20),
        new Session(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero), 103.20, 104.80, 102.40, 104.60),
        new Session(new DateTimeOffset(2026, 3, 6, 0, 0, 0, TimeSpan.Zero), 104.60, 106.90, 104.30, 106.40),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new DateTimeAxis(),
            VerticalAxis = new LinearAxis(),
        };

        chart.Series.Add(
            new CandlestickSeries<Session>
            {
                DisplayName = "Northwind",
                ItemsSource = Prices.Cast<Session>(),
                XSelector = session => session.Day,
                OpenSelector = session => session.Open,
                HighSelector = session => session.High,
                LowSelector = session => session.Low,
                CloseSelector = session => session.Close,
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
