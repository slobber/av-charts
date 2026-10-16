using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Elements;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The oscilloscope page: a triggered sweep of a live signal, with the controls a scope actually has.
internal static class OscilloscopePage
{
    /// <summary>A triggered sweep of a live signal, at a timebase and a gain the reader chooses.</summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// <para>
    /// A scope is not a strip chart. Its sweep starts where the signal crossed a level the reader chose, which is
    /// what makes a repeating wave stand still on the screen instead of sliding across it. So the window is
    /// placed by the trigger rather than by the clock: the newest rising crossing that is a whole sweep old
    /// becomes the left edge, and the sweep shows what has arrived since - a sweep the reader can read, rather
    /// than one that is still being drawn.
    /// </para>
    /// <para>
    /// The controls are the ones on the front of a scope: a timebase (how long a sweep is), a gain (how much of
    /// the signal a sweep covers) and a trigger level, and a switch that stops the display while the signal
    /// carries on being sampled. The graticule is ten divisions across and four either side of the middle.
    /// </para>
    /// <para>
    /// The readings are produced by a timer on the UI thread because this is a demo and not a device; a device
    /// pushes from its own thread, which is what <see cref="CartesianSeries{T}.Push"/> is for.
    /// </para>
    /// </remarks>
    public static DemoPage Create() => Page(
        "Oscilloscope",
        "A triggered sweep of a live signal, with a timebase, a gain and a trigger level.",
        Try("Try it: a scope's controls on a live trace", ScopePlayground()));

    /// <summary>The live trace with a scope's front panel beside it.</summary>
    /// <returns>The playground.</returns>
    private static Control ScopePlayground()
    {
        var across = new LinearAxis { Minimum = 0, Maximum = 0.05, Interval = 0.005 };
        var up = new LinearAxis { Minimum = -1, Maximum = 1, Interval = 0.25 };
        var chart = new CartesianChart { HorizontalAxis = across, VerticalAxis = up };

        var series = new LineSeries<Probe>
        {
            DisplayName = "Probe",
            ItemsSource = new List<Probe>(),
            XSelector = Probe => Probe.Seconds,
            YSelector = Probe => Probe.Reading,
            StrokeThickness = 1.5,
        };

        chart.Series.Add(series);

        // The level the sweep waits for is drawn on the graticule, so that the trigger is something the reader
        // can see rather than something they have to take on trust.
        var trigger = new Trigger();

        chart.Elements.Add(trigger);

        var report = new TextBlock { FontSize = 11, Opacity = 0.6 };
        var sweep = new Sweep(series, across, up, trigger, report);

        sweep.Fill();

        var timebases = new[] { 0.01, 0.05, 0.2 };
        var gains = new[] { 0.5, 1.0, 2.0 };
        var levels = new[] { -0.4, 0.0, 0.2, 0.6 };

        var playground = Playground(
            chart,
            Choices("Timebase", ["10 ms", "50 ms", "200 ms"], 1, picked => sweep.Timebase = timebases[picked]),
            Choices("Gain", ["±0.5", "±1", "±2"], 1, picked => sweep.Gain = gains[picked]),
            Choices("Trigger at", ["-0.4", "0", "0.2", "0.6"], 2, picked => sweep.Level = levels[picked]),
            OnOff("Sweeping", true, on => sweep.Running = on),
            Labelled("The sweep", report));

        // A scope nobody is looking at is a scope that can stop sweeping: the reading, the sampling and the sweep
        // all wait for the page to be on screen, and pick up where they were when it is again.
        WhileShown(playground, () => sweep.Running = true, () => sweep.Running = false);

        return playground;
    }

    /// <summary>One reading of the probe, with the moment it was taken at.</summary>
    /// <param name="Seconds">How far into the recording it is.</param>
    /// <param name="Reading">What the probe read.</param>
    private sealed record Probe(double Seconds, double Reading);

    /// <summary>The line across the graticule that the sweep waits for the signal to cross.</summary>
    private sealed class Trigger : ChartElement
    {
        private double _level;

        /// <summary>Where the line sits, in the units the vertical axis is drawn in.</summary>
        public double Level
        {
            get => _level;
            set
            {
                if (_level.Equals(value))
                {
                    return;
                }

                _level = value;
                Invalidate();
            }
        }

        /// <inheritdoc />
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(0), ChartValue.FromNumber(_level), out var at))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22)), 1, new DashStyle([4, 3], 0));

            context.DrawLine(pen, new Point(plot.Left, at.Y), new Point(plot.Right, at.Y));
        }
    }

    /// <summary>
    /// A probe sampled a thousand times a second, and the sweep of it that is on screen.
    /// </summary>
    /// <remarks>
    /// The sweep is the interesting part, and it is arithmetic rather than drawing: find the crossing the reader
    /// asked for, put the window there, and let the axes do the rest.
    /// </remarks>
    private sealed class Sweep
    {
        private const int Divisions = 10;

        /// <summary>Four milliseconds: the shortest a timer on this platform can be asked for, and enough.</summary>
        private static readonly TimeSpan Beat = TimeSpan.FromMilliseconds(4);

        private readonly LineSeries<Probe> _series;
        private readonly LinearAxis _across;
        private readonly LinearAxis _up;
        private readonly Trigger _trigger;
        private readonly TextBlock _report;
        private readonly DispatcherTimer _clock = new() { Interval = Beat };
        private readonly Stopwatch _since = Stopwatch.StartNew();
        private readonly List<Probe> _samples;

        private double _at;

        /// <summary>Where on the recording the wall clock was started at, so that the two can be read as one.</summary>
        /// <remarks>
        /// The readings carry the moment they were taken at, and the history the page opens with already reaches
        /// four sweeps along that clock. So the wall clock is an offset from where the recording had got to rather
        /// than a time of its own: read the other way, the first reading after the history would be stamped before
        /// the earliest one in it, and the trace would double back on itself.
        /// </remarks>
        private double _origin;

        private double _span = 0.05;
        private double _gain = 1.0;
        private double _level = 0.2;
        private double _crossing = double.NaN;
        private double _before = double.NaN;
        private double _sweeps;
        private bool _running = true;

        /// <summary>How far apart two samples are, in seconds: the probe's own rate, and the whole timebase is
        /// counted in these.</summary>
        private const double Step = 1.0 / 1_000;

        /// <summary>Creates a sweep driving a series, a pair of axes and the line the trigger sits on.</summary>
        /// <param name="series">The series to push to, whose source is the history.</param>
        /// <param name="across">The axis time runs along.</param>
        /// <param name="up">The axis the signal is measured up.</param>
        /// <param name="trigger">The line the sweep waits for the signal to cross.</param>
        /// <param name="report">Where to write what the sweep is doing.</param>
        public Sweep(LineSeries<Probe> series, LinearAxis across, LinearAxis up, Trigger trigger, TextBlock report)
        {
            _series = series;
            _across = across;
            _up = up;
            _trigger = trigger;
            _report = report;
            _samples = (List<Probe>)series.ItemsSource!;

            _clock.Tick += (_, _) => Arrive();
            Bound();
            Gain = _gain;
            Level = _level;
        }

        /// <summary>Keeps the series to the sweeps the trace can reach, and lets the rest go.</summary>
        /// <remarks>
        /// The series bounds itself rather than the page trimming its list: a scope left on overnight would
        /// otherwise hold every reading it ever took, since a plain list does not announce its own edits and a
        /// series that is not told goes on drawing and ranging what it has already read. Four sweeps of a probe
        /// sampling a thousand times a second is a count, so the count is what it is told.
        /// </remarks>
        private void Bound() => _series.MaxReadings = (int)Math.Round(_span * 4 / Step);

        /// <summary>How long one sweep covers, in seconds.</summary>
        public double Timebase
        {
            get => _span;
            set
            {
                _span = value;
                _across.Interval = value / Divisions;
                Bound();
                Place();
            }
        }

        /// <summary>How much of the signal a sweep covers, either side of the middle.</summary>
        public double Gain
        {
            get => _gain;
            set
            {
                _gain = value;
                _up.Minimum = -value;
                _up.Maximum = value;
                _up.Interval = value / 4;
            }
        }

        /// <summary>What the signal has to cross for a sweep to start, in the units of the vertical axis.</summary>
        public double Level
        {
            get => _level;
            set
            {
                _level = value;
                _trigger.Level = value;
                Place();
            }
        }

        /// <summary>Whether the display is following the signal or holding still.</summary>
        public bool Running
        {
            get => _running;
            set
            {
                // Applied whether it is a change or not: a page says "sweeping" every time it is put on screen and
                // "held" every time it is put away, and does not have to remember which it said last.
                _running = value;

                if (value)
                {
                    _at = Now();
                    _clock.Start();
                }
                else
                {
                    _clock.Stop();
                }

                Describe();
            }
        }

        /// <summary>Samples a few sweeps before anything is running, so the trace has a history to sweep.</summary>
        public void Fill()
        {
            var until = _span * 4;

            for (var at = 0.0; at < until; at += Step)
            {
                _samples.Add(new Probe(at, Signal(at)));
            }

            _at = until;
            _origin = until;
            _since.Restart();

            // Sampled, but not yet sweeping: the clock starts when the page is on screen, which is what keeps a
            // page nobody is looking at from costing anything.
            _clock.Stop();

            // The history went into the page's own list, which is a plain one and does not announce what is added
            // to it, so the series is told to read it again. This is the one thing the page still says about the
            // window: everything that arrives after this goes in through a push, and everything that leaves is the
            // series' own doing, since it is the one that knows the sweep's length.
            _series.InvalidateData();
            Place();
        }

        /// <summary>Samples everything the probe owes, and moves the sweep to the trigger.</summary>
        private void Arrive()
        {
            var now = Now();

            // What was missed while the tab was hidden is let go rather than pushed in one burst: a scope shows
            // the last few sweeps, not everything since the reader last looked at it.
            var owed = Math.Min(now - _at, _span * 4);

            if (owed < Step)
            {
                return;
            }

            for (var due = Step; due <= owed + Step / 2; due += Step)
            {
                _at += Step;
                _series.Push(new Probe(_at, Signal(_at)));
            }

            _at = Math.Max(_at, now - (_span * 4));

            Place();
        }

        /// <summary>What the recording's clock says now.</summary>
        /// <returns>The time, in seconds.</returns>
        private double Now() => _origin + _since.Elapsed.TotalSeconds;

        /// <summary>
        /// Puts the window on the newest crossing that is a whole sweep old, or lets it run free when the signal
        /// has not crossed the level at all.
        /// </summary>
        private void Place()
        {
            var newest = _samples.Count > 0 ? _samples[^1].Seconds : _at;
            var until = newest - _span;
            var found = double.NaN;

            for (var index = 1; index < _samples.Count; index++)
            {
                var was = _samples[index - 1].Reading;
                var isNow = _samples[index].Reading;

                if (was < _level && isNow >= _level && _samples[index].Seconds <= until)
                {
                    found = _samples[index].Seconds;
                }
            }

            if (!double.IsNaN(found))
            {
                if (!double.IsNaN(_crossing))
                {
                    // How often the signal crosses the level is what a scope is usually read for, so it is said
                    // rather than left to be counted off the graticule.
                    var period = found - _before;

                    if (period > 0)
                    {
                        _sweeps = 1 / period;
                    }
                }

                _before = found;
                _crossing = found;
                _across.Minimum = found;
            }
            else
            {
                // Nothing crossed the level, so the sweep free-runs: a scope with no trigger shows what is
                // arriving rather than nothing at all.
                _crossing = double.NaN;
                _across.Minimum = newest - _span;
            }

            _across.Maximum = _across.Minimum + _span;
            Describe();
        }

        /// <summary>Says what the sweep is doing, in words.</summary>
        private void Describe() =>
            _report.Text = _running
                ? $"{_span * 1_000:N0} ms a sweep, {_sweeps:N1} sweeps a second, {_samples.Count:N0} samples held."
                : $"held: {_span * 1_000:N0} ms a sweep, {_samples.Count:N0} samples held.";

        /// <summary>What the probe is looking at: a mains-shaped wave with a ripple and an occasional spike.</summary>
        /// <param name="seconds">When.</param>
        /// <returns>The reading.</returns>
        /// <remarks>
        /// Worked out rather than random, so that the same moment is always the same reading: a picture the build
        /// checks cannot be a picture that changes by itself.
        /// </remarks>
        private static double Signal(double seconds)
        {
            // A little under one division high, so that the trace sits inside the graticule at the gain the page
            // opens with; the spike goes over the top, which is what a glitch on a probe does.
            var mains = 0.8 * Math.Sin(2 * Math.PI * 50 * seconds);
            var ripple = 0.15 * Math.Sin(2 * Math.PI * 250 * seconds);
            var noise = 0.04 * Math.Sin(2 * Math.PI * 977 * seconds);
            var spike = Math.Abs((seconds % 0.5) - 0.25) < 0.002 ? 0.4 : 0.0;

            return mains + ripple + noise + spike;
        }
    }
}
