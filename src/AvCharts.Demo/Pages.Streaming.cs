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
    /// history it keeps, so the feed holds the last few seconds and lets the rest go. The axis is told where the
    /// window starts, which is what makes it look like something arriving rather than something growing.
    /// </para>
    /// <para>
    /// The readings are produced by a timer on the UI thread because this is a demo and not a device; a device
    /// pushes from its own thread, which is the case the push path was written for.
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
        var chart = new CartesianChart
        {
            // The window follows the feed rather than the feed fitting the window: the axis covers the last few
            // seconds, wherever the newest reading is.
            HorizontalAxis = new LinearAxis { Minimum = 0 },
            VerticalAxis = new LinearAxis { Minimum = -1.6, Maximum = 1.6, Interval = 0.4 },
        };

        var series = new LineSeries<Live>
        {
            DisplayName = "Live",
            ItemsSource = new List<Live>(),
            XSelector = Live => Live.Seconds,
            YSelector = Live => Live.Reading,
            StrokeThickness = 1.5,
        };

        chart.Series.Add(series);

        var report = new TextBlock { FontSize = 11, Opacity = 0.6 };
        var feed = new Feed(series, (LinearAxis)chart.HorizontalAxis, report);

        // Filled before anything is running, so that the page has a picture the moment it is opened - and so that
        // the picture the build checks is the same one every time.
        feed.Fill();

        var rates = new[] { 1_000, 200, 50 };
        var windows = new[] { 2.0, 5.0, 10.0 };

        return Playground(
            chart,
            Choices("Readings a second", ["1000", "200", "50"], 0, picked => feed.Rate = rates[picked]),
            Choices("Window", ["2 s", "5 s", "10 s"], 1, picked => feed.Window = windows[picked]),
            OnOff("Arriving", true, on => feed.Running = on),
            Labelled("What it is doing", report));
    }

    /// <summary>One reading of the live feed, with the moment it was taken at.</summary>
    /// <param name="Seconds">How far into the recording it is.</param>
    /// <param name="Reading">What the signal was.</param>
    private sealed record Live(double Seconds, double Reading);

    /// <summary>
    /// A producer pushing readings into a series one millisecond apart, and the window of them the page keeps.
    /// </summary>
    /// <remarks>
    /// The shape a host writes for a live chart: something produces readings on its own schedule, the series is
    /// handed them, and the page decides how much of the past is worth holding. What is not the page's business
    /// is when the chart draws: <c>Push</c> says "here is a reading", and the chart decides when to spend a frame
    /// on it.
    /// </remarks>
    private sealed class Feed
    {
        /// <summary>Four milliseconds: the shortest a timer on this platform can be asked for, and enough.</summary>
        private static readonly TimeSpan Beat = TimeSpan.FromMilliseconds(4);

        private readonly LineSeries<Live> _series;
        private readonly LinearAxis _across;
        private readonly TextBlock _report;
        private readonly DispatcherTimer _clock = new() { Interval = Beat };
        private readonly Stopwatch _since = Stopwatch.StartNew();
        private readonly List<Live> _samples;

        private double _at;
        private double _window = 5;
        private int _rate = 1_000;
        private bool _running = true;
        private double _arrived;
        private double _perSecond;

        /// <summary>Creates a feed pushing into a series, with the window it covers written on an axis.</summary>
        /// <param name="series">The series to push to, whose source is the window.</param>
        /// <param name="across">The axis the window is shown on.</param>
        /// <param name="report">Where to write what the feed is doing.</param>
        public Feed(LineSeries<Live> series, LinearAxis across, TextBlock report)
        {
            _series = series;
            _across = across;
            _report = report;
            _samples = (List<Live>)series.ItemsSource!;

            _clock.Tick += (_, _) => Arrive();
        }

        /// <summary>How many readings a second the feed produces; a thousand at the fastest.</summary>
        public int Rate
        {
            get => _rate;
            set => _rate = value;
        }

        /// <summary>How many seconds of history are kept, on screen and on the page.</summary>
        public double Window
        {
            get => _window;
            set
            {
                _window = value;
                LetGo();
                Cover();
            }
        }

        /// <summary>Whether readings are arriving at all.</summary>
        public bool Running
        {
            get => _running;
            set
            {
                if (_running == value)
                {
                    return;
                }

                _running = value;

                if (value)
                {
                    // Picking the clock up again after a pause: the readings that happened while it was stopped
                    // are not owed, so the feed starts from now rather than catching up in one burst.
                    _at = _since.Elapsed.TotalSeconds;
                    _clock.Start();
                }
                else
                {
                    _clock.Stop();
                }

                Cover();
            }
        }

        /// <summary>Fills the window from the beginning of time and starts the clock.</summary>
        public void Fill()
        {
            var step = 1.0 / _rate;

            for (var at = 0.0; at < _window; at += step)
            {
                _samples.Add(new Live(at, Signal(at)));
            }

            _at = _window;
            _since.Restart();
            _clock.Start();
            Cover();
        }

        /// <summary>Pushes everything the feed owes since the last beat.</summary>
        private void Arrive()
        {
            var now = _since.Elapsed.TotalSeconds;
            var step = 1.0 / _rate;

            // A page that was hidden, or a machine that hiccuped, owes more readings than a window can hold:
            // what it missed is let go rather than pushed in one unreadable burst.
            var owed = Math.Min(now - _at, _window);

            if (owed < step)
            {
                return;
            }

            for (var due = step; due <= owed + step / 2; due += step)
            {
                _at += step;
                _series.Push(new Live(_at, Signal(_at)));
                _arrived++;
            }

            _at = Math.Max(_at, now - _window);
            LetGo();

            var elapsed = _since.Elapsed.TotalSeconds;

            _perSecond = elapsed > 1 ? _arrived / elapsed : _rate;
            Cover();
        }

        /// <summary>Drops the readings that have scrolled out of the window.</summary>
        private void LetGo()
        {
            var from = _at - _window;
            var gone = 0;

            while (gone < _samples.Count && _samples[gone].Seconds < from)
            {
                gone++;
            }

            if (gone > 0)
            {
                _samples.RemoveRange(0, gone);
            }
        }

        /// <summary>Says where the window starts, and what the feed is doing, in words.</summary>
        private void Cover()
        {
            _across.Minimum = Math.Max(0, _at - _window);
            _report.Text =
                $"{_rate:N0} a second asked for, {_perSecond:N0} arriving; "
                + $"{_samples.Count:N0} readings in the window.";
        }

        /// <summary>What the feed is looking at: a slow wander with a faster ripple on it.</summary>
        /// <param name="seconds">When.</param>
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
}
