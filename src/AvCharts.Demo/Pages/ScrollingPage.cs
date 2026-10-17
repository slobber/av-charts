using Avalonia.Controls;
using Avalonia.Layout;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The Scrolling page: a window onto a day of readings, and the bars that say where it is and drag it along.
// The page is called Scrolling rather than Navigation because Avalonia has a NavigationPage of its own: a second
// one under that name is an ambiguous reference for every file that has both namespaces open - the same trap the
// library's Series ran into against AvCharts.Series, and settled the same way, by renaming the thing that is ours.
internal static class ScrollingPage
{
    /// <summary>Midnight, which is where the readings start.</summary>
    private static readonly DateTimeOffset Midnight = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The moment the window opens at, six hours into the day.</summary>
    private static readonly DateTimeOffset Morning = Midnight.AddHours(6);

    /// <summary>What the bars under the plot, and the room between them, take out of the frame an example stands in.</summary>
    /// <remarks>
    /// A scroll bar eight pixels thick, an overview forty, and six pixels between each of them and the plot.
    /// </remarks>
    private const double BarsRoom = 60;

    /// <summary>A window on part of a day of readings, with a bar under the plot and one beside it.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var chart = Chart(6, out _, out _);
        var board = Board(chart);

        // The frame an example stands in is 240 pixels tall and the bars under the plot take sixty of them, so the
        // chart is given the rest: one given the whole frame would hang its bars out of the frame and over the block
        // below. Only this one is sized here - a playground passes its own size down, so that a reader can drag the
        // corner and watch the chart, the bars and the plot answer to it.
        chart.Height = ExampleHeight - BarsRoom;

        return Page(
            "Scrolling",
            "A window onto a day of readings: a bar that says where it is, and an overview to pick it from.",
            Example("Six hours of a morning, out of a day of readings", board),
            Example("A hundred thousand readings, sampled to the pixels there are", HundredThousand()),
            Try("Try it: a wider window, or a longer recording, with the thumb to match", NavigationPlayground()));
    }

    /// <summary>
    /// A signal a hundred thousand readings long, shown through a window of the first half.
    /// </summary>
    /// <returns>The chart.</returns>
    /// <remarks>
    /// The volume is the point: two readings per pixel column is not a curve a reader can see, so the series is
    /// thinned to the columns the plot has, keeping the lowest and the highest reading in each. The page keeps it
    /// beside the scrolling example because that is what a recording of this length is read through - a window.
    /// </remarks>
    private static CartesianChart HundredThousand()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 50_000 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
        };

        chart.Series.Add(new LineSeries<double>
        {
            DisplayName = "Signal",
            ItemsSource = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))],
            YSelector = value => value,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        });

        return chart;
    }

    /// <summary>The same chart with knobs on the two things a bar is about: how much is on screen, and how much
    /// there is to be on screen out of.</summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// The second knob is the one worth turning: a week of readings with a three hour window is a window of a
    /// fifty-sixth of the readings, which is a thumb a fiftieth of the track - and the shortest a thumb is allowed
    /// to get. That clamp is the whole reason it exists, and it shows here as a thumb that stops shrinking.
    /// </remarks>
    private static Control NavigationPlayground()
    {
        var chart = Chart(3, out var across, out var series);
        var board = Board(chart);

        var windows = new[] { "an hour", "three hours", "six hours" };
        var hours = new[] { 1, 3, 6 };
        var recordings = new[] { "a day", "a week" };

        void Window(string picked)
        {
            // Anchored at the morning the window opens at rather than centred, so that the knob changes the length
            // of the window and the length of the thumb, and not where the window is along the readings.
            across.Maximum = At(Morning.AddHours(hours[Array.IndexOf(windows, picked)]));
        }

        void Recording(string picked) =>
            series.ItemsSource = Samples(
                picked == "a week" ? 7 * 24 * 60 : 24 * 60);

        return Playground(
            board,
            Choice("Window", windows, "three hours", Window),
            Choice("Readings", recordings, "a day", Recording));
    }

    /// <summary>The chart both blocks are drawn from: a day of temperatures behind a window of its own.</summary>
    /// <param name="hours">How many hours of the day are on screen to begin with.</param>
    /// <param name="across">The horizontal axis, so that a knob can move the window on it.</param>
    /// <param name="series">The series, so that a knob can change how much has been recorded.</param>
    /// <returns>The chart.</returns>
    /// <remarks>
    /// Both scales are pinned rather than fitted, which is the case a bar is for: a long recording of which a
    /// stretch is on screen. A fitted scale covers all of the readings, so its window is the whole of them and
    /// there is nothing for a reader to be lost in - which is why the gallery's other pages have no bar.
    /// </remarks>
    private static CartesianChart Chart(double hours, out DateTimeAxis across, out LineSeries<Sample> series)
    {
        // A date axis counts milliseconds from 1970, which is what the window is written in.
        across = new DateTimeAxis
        {
            Minimum = At(Morning),
            Maximum = At(Morning.AddHours(hours)),
            Title = "Time of day",
        };

        series = new LineSeries<Sample>
        {
            DisplayName = "Degrees",
            ItemsSource = Samples(24 * 60),
            XSelector = sample => sample.When,
            YSelector = sample => sample.Reading,
            StrokeThickness = 1.5,
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = across,

            // A band of the day's temperatures rather than all of them: the readings run from six degrees to
            // eighteen, and a window of ten to eighteen is a band with the day's cool half below it - which is what
            // gives the bar beside the plot something to scroll to, without cutting the morning's curve off at the
            // top of the plot, which the first version of this page did.
            VerticalAxis = new LinearAxis { Minimum = 10, Maximum = 18, Title = "Degrees" },
        };

        chart.Series.Add(series);
        return chart;
    }

    /// <summary>The chart with a bar under it and a bar beside it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>What goes in the block.</returns>
    private static Control Board(CartesianChart chart)
    {
        // Three of them, and each is about one thing: the bar under the plot says where the window is among the
        // readings, the overview under that draws the whole recording and takes a window by its ends as well as by
        // its middle - which is the half a bar cannot do - and the bar beside the plot does for the other dimension
        // what the first one does for this. The same pair works up the vertical scale; the page shows the three
        // rather than four so that the plot keeps some room.
        var under = new ChartScrollBar { Chart = chart };
        var beside = new ChartScrollBar { Chart = chart, Orientation = Orientation.Vertical };
        var whole = new ChartOverview { Chart = chart };

        var board = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,6,Auto"),
            RowDefinitions = new RowDefinitions("*,6,Auto,6,Auto"),
        };

        Grid.SetRow(chart, 0);
        Grid.SetColumn(chart, 0);
        Grid.SetRow(under, 2);
        Grid.SetColumn(under, 0);
        Grid.SetRow(whole, 4);
        Grid.SetColumn(whole, 0);
        Grid.SetRow(beside, 0);
        Grid.SetColumn(beside, 2);

        board.Children.Add(chart);
        board.Children.Add(under);
        board.Children.Add(whole);
        board.Children.Add(beside);
        return board;
    }

    /// <summary>The readings: a temperature that walks a day, one a minute, for as many minutes as are asked for.</summary>
    /// <param name="minutes">How many minutes have been recorded.</param>
    /// <returns>The readings.</returns>
    /// <remarks>
    /// Worked out rather than written down, so that a day of them is a day rather than a list of numbers, and so
    /// that a week of them is the same day seven times over - which is what a week of a temperature is.
    /// </remarks>
    private static Sample[] Samples(int minutes) =>
        [.. Enumerable.Range(0, minutes).Select(minute => new Sample(
            Midnight.AddMinutes(minute),
            Math.Round(12 + (6 * Math.Sin((minute - 300) / 240.0)), 2)))];

    /// <summary>A moment on the axis' own scale: milliseconds from 1970, which is what a date axis counts in.</summary>
    /// <param name="when">The moment.</param>
    /// <returns>The value the axis' window is written with.</returns>
    private static double At(DateTimeOffset when) => DateTimeAxis.ToMilliseconds(when);

    /// <summary>One reading: when it was taken, and what it was.</summary>
    /// <param name="When">The moment.</param>
    /// <param name="Reading">The temperature.</param>
    private sealed record Sample(DateTimeOffset When, double Reading);
}
