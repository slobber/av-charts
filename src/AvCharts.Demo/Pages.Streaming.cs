using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo;

// The streaming page: a chart drawn while a feed keeps arriving, and the knobs a live feed wants.
public static partial class Pages
{
    /// <summary>A chart fed while it draws: a thousand readings a second, pushed a batch at a time.</summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// <para>
    /// This is the library's push path, which is what it is for: a reading is handed over with
    /// <see cref="CartesianSeries{T}.Push"/> from wherever it came from - a device thread, a feed callback, a
    /// worker - the series holds it, and the chart applies the batch once a frame. A thousand readings a second
    /// therefore cost one repaint a frame rather than a thousand, and the values are only ever written on the
    /// thread the chart draws on.
    /// </para>
    /// <para>
    /// The window is the page's own business rather than the chart's: a rolling chart has to decide how much
    /// history it keeps, so the page holds the last few seconds and lets the rest go. What it keeps is what the
    /// axis covers, so the time across the screen is the time the window holds - and the newest reading is always
    /// at the right-hand edge, because that is where the recording has got to.
    /// </para>
    /// <para>
    /// The readings come from <see cref="Feed"/>, which is told how much time has gone by rather than reading a
    /// clock of its own; a timer on the UI thread does the telling, because this is a demo and not a device. A
    /// device pushes from its own thread, which is the case the push path was written for.
    /// </para>
    /// </remarks>
    internal static DemoPage Streaming() => Page(
        "Streaming",
        "A thousand readings a second, arriving while the chart draws.",
        Try("Try it: a live feed, at the rate and the window you ask for", StreamingPlayground()));

    /// <summary>
    /// The live chart with knobs on: how fast the readings arrive, how much history is kept, and a switch.
    /// </summary>
    /// <returns>The playground.</returns>
    private static Control StreamingPlayground()
    {
        var samples = new List<FeedSample>();
        var chart = new CartesianChart
        {
            // Time across, read as the clock reads it: the axis steps in seconds and writes its labels as times
            // rather than as a count of seconds, which is what a feed is read against. The clock is the
            // recording's own - the reading taken three seconds in is at 00:00:03 - because a feed of unknown
            // length is read from its start, and because a picture the build checks cannot be a picture that
            // depends on when the build ran.
            HorizontalAxis = new TimeSpanAxis(),
            VerticalAxis = new LinearAxis { Minimum = -1.6, Maximum = 1.6, Interval = 0.4 },
        };

        var series = new LineSeries<FeedSample>
        {
            DisplayName = "Live",
            ItemsSource = samples,
            XSelector = reading => reading.At,
            YSelector = reading => reading.Value,
            StrokeThickness = 1.5,
        };

        chart.Series.Add(series);

        var report = new TextBlock { FontSize = 11, Opacity = 0.6 };
        var feed = new Feed(Signal);
        var since = Stopwatch.StartNew();

        // A window of readings before anything is running: the page has a picture the moment it is opened, and the
        // picture the build checks is the same one every time.
        samples.AddRange(feed.Advance(TimeSpan.FromSeconds(feed.Window)));
        since.Restart();
        Describe();

        var clock = new DispatcherTimer { Interval = Pulse };

        clock.Tick += (_, _) => Arrive();
        clock.Start();

        void Arrive()
        {
            foreach (var reading in feed.Advance(Pulse))
            {
                series.Push(reading);
            }

            LetGo();
            Describe();
        }

        // Drops the readings that have scrolled out of the window.
        void LetGo()
        {
            var oldest = feed.Oldest;
            var gone = 0;

            while (gone < samples.Count && samples[gone].At < oldest)
            {
                gone++;
            }

            if (gone > 0)
            {
                samples.RemoveRange(0, gone);
            }
        }

        // Says what the window holds, and what the feed is doing, in words.
        void Describe()
        {
            var seconds = since.Elapsed.TotalSeconds;
            var arriving = seconds > 1 ? feed.Arrived / seconds : feed.Rate;

            report.Text =
                $"{feed.Rate:N0} a second asked for, {arriving:N0} arriving; "
                + $"{samples.Count:N0} readings in the last {feed.Window:0} s.";
        }

        var rates = new[] { 1_000, 200, 50 };
        var windows = new[] { 2.0, 5.0, 10.0 };

        return Playground(
            chart,
            Choices("Readings a second", ["1000", "200", "50"], 0, picked => feed.Rate = rates[picked]),
            Choices("Window", ["2 s", "5 s", "10 s"], 1, picked => { feed.Window = windows[picked]; LetGo(); Describe(); }),
            OnOff("Arriving", true, on => { feed.Running = on; Describe(); }),
            Labelled("What it is doing", report));
    }

    /// <summary>Four milliseconds: the shortest a timer on this platform can be asked for, and enough.</summary>
    private static readonly TimeSpan Pulse = TimeSpan.FromMilliseconds(4);

    /// <summary>What the feed is looking at: a slow wander with a faster ripple on it.</summary>
    /// <param name="seconds">When, in seconds since the recording started.</param>
    /// <returns>The reading.</returns>
    /// <remarks>
    /// Worked out rather than random, so that the same moment is always the same reading: a picture the build
    /// checks cannot be a picture that changes by itself.
    /// </remarks>
    private static double Signal(double seconds) =>
        (0.7 * Math.Sin(2 * Math.PI * 0.6 * seconds))
        + (0.3 * Math.Sin(2 * Math.PI * 5.3 * seconds))
        + (0.1 * Math.Sin(2 * Math.PI * 23 * seconds));
}
