using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The Pie page: a whole cut into shares, and the playground that turns it round.
internal static class PiePage
{
    /// <summary>A circle of wedges, one per share.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var chart = Shares(out _);

        return Page(
            "Pie",
            "A whole divided into shares: one wedge per reading, sized by what that reading is worth.",
            Example("Where the sales came from", chart),
            Try("Try it: the same shares, drawn the way you ask", PiePlayground()));
    }

    /// <summary>
    /// The pie with knobs on: the hole in the middle, which way round it runs, and where the first slice starts.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// The three knobs are the three things a pie is read by: a doughnut is the same picture with its middle
    /// taken out, and the two that turn the circle are how a reader makes the slice they care about stand
    /// where they are looking - a pie has no axes to zoom, so where its slices start is the whole of the
    /// question "which one is first".
    /// </remarks>
    private static Control PiePlayground()
    {
        var chart = Shares(out var series);

        var holes = new[] { "none", "half", "two thirds" };
        var widths = new[] { 0.0, 0.5, 0.67 };
        var directions = new[] { "anticlockwise", "clockwise" };
        var places = new[] { "three o'clock", "twelve o'clock", "six o'clock", "nine o'clock" };
        var angles = new[] { 0.0, 90.0, 180.0, 270.0 };

        return Playground(
            chart,
            Choice(
                "The hole",
                holes,
                holes[0],
                picked => series.InnerRadius = widths[Array.IndexOf(holes, picked)]),
            Choice(
                "Direction",
                directions,
                directions[0],
                picked => series.Clockwise = picked == directions[1]),
            Choice(
                "The first slice starts",
                places,
                places[0],
                picked => series.StartAngle = angles[Array.IndexOf(places, picked)]));
    }

    /// <summary>A pie of the shares the page and its playground are drawn from.</summary>
    /// <param name="series">The series, so that a playground can turn its knobs.</param>
    /// <returns>The chart.</returns>
    /// <remarks>
    /// Nothing is said about the colours: every wedge takes a slot of the chart's palette in the order the
    /// readings were recorded, which is what a pie is coloured by - the theme's ten colours, and round again
    /// when there are more wedges than that.
    /// </remarks>
    private static PieChart Shares(out PieSeries<Share> series)
    {
        var chart = new PieChart();

        series = new PieSeries<Share>
        {
            DisplayName = "Sales",
            ItemsSource = Regions(),
            ValueSelector = share => share.Value,
            NameSelector = share => share.Name,
        };

        chart.Series.Add(series);
        return chart;
    }

    /// <summary>Where the sales came from, which is what the wedges are worth.</summary>
    /// <returns>The shares.</returns>
    private static Share[] Regions() =>
    [
        new("North", 42),
        new("South", 28),
        new("East", 17),
        new("West", 13),
    ];

    /// <summary>One share of the whole: what it is called, and what it is worth.</summary>
    /// <param name="Name">What it is called.</param>
    /// <param name="Value">What it is worth.</param>
    private sealed record Share(string Name, double Value);
}
