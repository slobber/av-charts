using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The streaming page: a chart drawn while a feed keeps arriving, and the knobs a live feed wants.
internal static class StreamingPage
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
    public static DemoPage Create() => Page(
        "Streaming",
        "A thousand readings a second, arriving while the chart draws.",
        Try("Try it: a live feed, at the rate and the window you ask for", StreamingPlayground()));

    /// <summary>
    /// The live chart with knobs on: how fast the readings arrive, how much history is kept, and a switch.
    /// </summary>
    /// <returns>The playground.</returns>
    private static Control StreamingPlayground()
    {
        var across = new TimeSpanAxis();
        var samples = new List<FeedSample>();
        var chart = new CartesianChart
        {
            // Time across, read as the clock reads it: the axis steps in seconds and writes its labels as times
            // rather than as a count of seconds, which is what a feed is read against. The clock is the
            // recording's own - the reading taken three seconds in is at 00:00:03 - because a feed of unknown
            // length is read from its start, and because a picture the build checks cannot be a picture that
            // depends on when the build ran.
            HorizontalAxis = across,
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

        // A reader who takes the window - by dragging it, or by turning the wheel on it - keeps it, and the feed
        // takes it back a few seconds after they have stopped. The clock the wait is measured on is the page's own,
        // and the check happens on every beat, so the window goes back within a frame of the wait being over.
        var resumeAt = TimeSpan.Zero;

        // When the last beat was, so that each beat knows what it owes.
        var beatAt = TimeSpan.Zero;

        var rest = TimeSpan.FromSeconds(3);

        // What is arriving, measured a second at a time.
        var counted = 0.0;
        var measuredAt = TimeSpan.Zero;
        var arriving = (double)feed.Rate;

        void Rest() => resumeAt = since.Elapsed + rest;

        void Take() => resumeAt = TimeSpan.Zero;

        bool Resting() => since.Elapsed >= resumeAt;

        chart.PointerPressed += (_, _) => Rest();
        chart.PointerWheelChanged += (_, _) => Rest();
        chart.PointerMoved += (_, e) =>
        {
            // Held down is dragging the plot; a pointer merely resting over it is not an operation.
            if (e.GetCurrentPoint(chart).Properties.IsLeftButtonPressed)
            {
                Rest();
            }
        };

        // A window of readings before anything is running: the page has a picture the moment it is opened, and the
        // picture the build checks is the same one every time.
        samples.AddRange(feed.Advance(TimeSpan.FromSeconds(feed.Window)));
        since.Restart();
        Describe();

        var clock = new DispatcherTimer { Interval = Pulse };

        clock.Tick += (_, _) => Arrive();

        void Arrive()
        {
            // The feed is told how much time has really gone by, not how much the timer was set for: a timer that
            // is asked to beat every four milliseconds and manages twenty beats a second would otherwise record a
            // fifth of a second a second, and the window would crawl along behind the clock.
            var now = since.Elapsed;
            var by = now - beatAt;

            beatAt = now;

            foreach (var reading in feed.Advance(by))
            {
                series.Push(reading);
            }

            Follow();
            LetGo();
            Describe();
        }

        // The window is the last few seconds of the recording, wherever a drag has left it. Dragging a live chart
        // writes the window by hand, and an axis that has been told where its two ends are stops following the
        // readings - which is a live chart that has quietly stopped being live. Letting the window go puts it back
        // where the readings are, whether or not any are still arriving: the switch is about the feed, and the
        // window is about the reader having stopped moving.
        void Follow()
        {
            if (!Resting())
            {
                return;
            }

            across.Minimum = null;
            across.Maximum = null;
        }

        // Drops the readings that have scrolled out of the window.
        void LetGo()
        {
            // Nothing is let go while the window is the reader's: what they have scrolled back over is what they
            // are looking at, and it is kept until the rest is over.
            if (!Resting())
            {
                return;
            }

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
            var now = since.Elapsed;

            // What is arriving is measured over the last second rather than since the page was opened: an average
            // that still remembers the time the feed was stopped says nothing about what it is doing now.
            if (now - measuredAt >= TimeSpan.FromSeconds(1))
            {
                arriving = (feed.Arrived - counted) / (now - measuredAt).TotalSeconds;
                counted = feed.Arrived;
                measuredAt = now;
            }

            report.Text =
                $"{feed.Rate:N0} a second asked for, {arriving:N0} arriving; "
                + $"{samples.Count:N0} readings in the last {feed.Window:0} s.";
        }

        var rates = new[] { 1_000, 200, 50 };
        var windows = new[] { 2.0, 5.0, 10.0 };

        var playground = Playground(
            chart,
            Choices("Readings a second", ["1000", "200", "50"], 0, picked => feed.Rate = rates[picked]),
            Choices(
                "Window",
                ["2 s", "5 s", "10 s"],
                1,
                picked =>
                {
                    feed.Window = windows[picked];
                    Follow();
                    LetGo();
                    Describe();
                }),
            OnOff(
                "Arriving",
                true,
                on =>
                {
                    feed.Running = on;

                    if (on)
                    {
                        // Asking for readings again is asking to watch them arrive, so the window goes back to the
                        // feed at once rather than after a rest. Stopping is not an operation on the window: a
                        // reader who stops the feed to look back at something has not asked for it to move.
                        Take();
                        Follow();
                    }

                    Describe();
                }),
            Labelled("What it is doing", report));

        // The clock runs while this page is the page being looked at, and not a moment longer: leaving the page
        // stops the feed, and coming back picks it up where it left off - the readings nobody was there for are
        // readings nobody is owed, so the window carries on rather than jumping.
        WhileShown(
            playground,
            () => { feed.Running = true; clock.Start(); Describe(); },
            () => { clock.Stop(); feed.Running = false; Describe(); });

        return playground;
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
