using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The Candles page: a price a session, drawn as a candle, and the playground that takes it apart.
internal static class CandlesPage
{
    /// <summary>A month of prices, drawn as candles.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var chart = CandleChart(Sessions(), out _);

        return Page(
            "Candles",
            "A price a session: a body from the open to the close, and a wick from the low to the high.",
            Example("Twenty-four sessions of a price", chart),
            Try("Try it: the same sessions, drawn the way you ask", CandlePlayground()),
            Sourced("A week of prices as bars, with a tick at the open and one at the close", "Candles/HighLowCloseBars"));
    }

    /// <summary>
    /// The candles with knobs on: how much of its band a candle takes, how wide one may grow, and whether it is
    /// given anything but its close.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// The last knob is the one worth turning: given the close alone, the series draws a line of flat candles
    /// rather than nothing at all. A price that a feed reports one number of is still a price.
    /// </remarks>
    private static Control CandlePlayground()
    {
        var chart = CandleChart(Sessions(), out var series);

        var widths = new[] { "40%", "65%", "90%" };
        var ratios = new[] { 0.4, 0.65, 0.9 };
        var caps = new double?[] { null, 8 };

        void Priced(bool given)
        {
            series.OpenSelector = given ? session => session.Open : null;
            series.HighSelector = given ? session => session.High : null;
            series.LowSelector = given ? session => session.Low : null;
        }

        return Playground(
            chart,
            Choice(
                "Candle width",
                widths,
                widths[1],
                picked => series.BandWidthRatio = ratios[Array.IndexOf(widths, picked)]),
            Choices("Widest candle", ["no cap", "8 px"], 0, picked => series.MaxBandWidth = caps[picked]),
            OnOff("Open, high and low", true, Priced));
    }

    /// <summary>A chart of one series of candles over the sessions.</summary>
    /// <param name="sessions">The sessions.</param>
    /// <param name="series">The series, so that a playground can turn its knobs.</param>
    /// <returns>The chart.</returns>
    /// <remarks>
    /// <para>
    /// Nothing is said about the colours, and that is the point of the small print: a candle that closed at or
    /// above where it opened is drawn in the colour the theme keeps for a price that went up, and one that closed
    /// below in the colour it keeps for a price that went down - so a host that reskins its application recolours
    /// the candles along with everything else, and the page has no opinion about it.
    /// </para>
    /// <para>
    /// The vertical scale is left alone for the same kind of reason: prices stand in the window they reach, not
    /// in the window they reach down to. A column is a distance from the axis' zero; a candle is a price, and an
    /// axis of prices that insisted on zero would draw a month of a hundred-pound share as a flat line at the top.
    /// </para>
    /// </remarks>
    private static CartesianChart CandleChart(Session[] sessions, out CandlestickSeries<Session> series)
    {
        var chart = new CartesianChart
        {
            // Real dates, so that the axis reads as a calendar: the candles stand a day apart because the readings
            // are a day apart, and each label says which day it was.
            HorizontalAxis = new DateTimeAxis(),
            VerticalAxis = new LinearAxis(),
        };

        series = new CandlestickSeries<Session>
        {
            DisplayName = "Northwind",
            ItemsSource = sessions,
            XSelector = session => session.Day,
            OpenSelector = session => session.Open,
            HighSelector = session => session.High,
            LowSelector = session => session.Low,
            CloseSelector = session => session.Close,
        };

        chart.Series.Add(series);
        return chart;
    }

    /// <summary>The sessions the page and its playground are drawn from.</summary>
    /// <returns>The sessions, oldest first.</returns>
    /// <remarks>
    /// Worked out rather than written down, so that the page is a price that wanders rather than a list of
    /// numbers, and so that it is the same price on every machine and every run. Each session opens where the
    /// last one closed, which is what makes these candles one price rather than four unrelated readings.
    /// </remarks>
    private static Session[] Sessions()
    {
        var first = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero);
        var sessions = new List<Session>();
        var price = 104.0;

        for (var day = 0; day < 24; day++)
        {
            var open = Math.Round(price, 2);
            var close = Math.Round(open + (6 * Math.Sin(day * 0.9)) + (2 * Math.Sin(day * 2.7)), 2);
            var high = Math.Round(Math.Max(open, close) + 1.4 + (0.6 * (day % 4)), 2);
            var low = Math.Round(Math.Min(open, close) - 1.1 - (0.7 * (day % 3)), 2);

            sessions.Add(new Session(first.AddDays(day), open, high, low, close));
            price = close;
        }

        return [.. sessions];
    }

    /// <summary>One session: the day it was, and the four prices it was worth.</summary>
    /// <param name="Day">The day.</param>
    /// <param name="Open">Where it opened.</param>
    /// <param name="High">The highest it reached.</param>
    /// <param name="Low">The lowest it reached.</param>
    /// <param name="Close">Where it closed.</param>
    private sealed record Session(DateTimeOffset Day, double Open, double High, double Low, double Close);
}
