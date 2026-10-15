namespace AvCharts.Demo;

/// <summary>One reading of a feed, with the moment it is about.</summary>
/// <param name="At">Milliseconds since the recording started, which the page turns into the instant it was
/// taken at.</param>
/// <param name="Value">What the signal was, or not-a-number for a moment nothing was recorded.</param>
public readonly record struct FeedSample(double At, double Value);

/// <summary>
/// Where a live chart's readings come from: a producer working at a rate, against one clock.
/// </summary>
/// <remarks>
/// <para>
/// The feed keeps the recording's clock - milliseconds since it started - and is <em>told</em> how much wall time
/// has gone by rather than reading a clock of its own. That is what makes it testable: hand it five seconds and
/// it hands back the five thousand readings that are due.
/// </para>
/// <para>
/// It is public, and driven rather than self-starting, because of the bug that got it written. It used to keep
/// two clocks - the readings were stamped on the recording's and the due readings worked out on the stopwatch -
/// and they disagreed by a window, so nothing arrived at all until the switch was flipped twice, and the first
/// reading that did arrive was drawn as a straight line across the chart to reach it.
/// </para>
/// <para>
/// What it does not do is overreach: it hands the readings over and says how far back is worth keeping. Pushing
/// them at a series is the page's business, because pushing is the chart's own path, and so is dropping what has
/// gone off the end.
/// </para>
/// </remarks>
public sealed class Feed
{
    private readonly Func<double, double> _signal;
    private readonly List<FeedSample> _due = [];

    // The recording's own clock, in ticks, and the wall time that is owed. Both are ticks and both are counted
    // rather than accumulated from a rate: the rate is a setting a reader can change while the recording runs,
    // and a clock worked out from the number of readings divided by the rate moves every reading in the window
    // the moment the setting changes - which is a chart that tears itself in half.
    private long _ticks;
    private long _owed;

    /// <summary>Creates a feed reading from a signal, which is given the recording time in seconds.</summary>
    /// <param name="signal">What the probe reads at a moment of the recording.</param>
    public Feed(Func<double, double> signal) => _signal = signal;

    /// <summary>How many readings a second the feed produces; a thousand by default.</summary>
    public int Rate { get; set; } = 1_000;

    /// <summary>How many seconds of history there are worth keeping.</summary>
    public double Window { get; set; } = 5;

    /// <summary>
    /// Whether readings are arriving. Stopped, the clock is not owed anything: time that went by while the feed
    /// was stopped is time nobody was recording, so picking up again carries on from the last reading.
    /// </summary>
    public bool Running { get; set; } = true;

    /// <summary>How many readings have been handed over.</summary>
    public double Arrived { get; private set; }

    /// <summary>The time of the newest reading, in milliseconds since the recording started.</summary>
    public double At => _ticks / (double)TimeSpan.TicksPerMillisecond;

    /// <summary>The time before which a reading is no longer worth keeping.</summary>
    public double Oldest => Math.Max(0, At - (Window * 1_000));

    /// <summary>Hands over every reading that is due for that much wall time.</summary>
    /// <param name="by">How much time has gone by since the last call.</param>
    /// <returns>The readings, oldest first; empty when none are due.</returns>
    /// <remarks>
    /// The list is the feed's own and is filled again by the next call, which is deliberate: a thousand readings a
    /// second is not a thousand lists a second. Read it before asking for more.
    /// </remarks>
    public IReadOnlyList<FeedSample> Advance(TimeSpan by)
    {
        _due.Clear();

        if (!Running)
        {
            _owed = 0;
            return _due;
        }

        // Time is counted in ticks and readings in whole steps of them, rather than by adding a thousandth of a
        // second over and over: a thousandth added five thousand times is not five seconds, and a window that
        // quietly loses a reading every few seconds is a window that does not add up. A rate that does not divide
        // a second into whole ticks rounds down, which is the price of counting rather than accumulating.
        var step = TimeSpan.TicksPerSecond / Rate;

        _owed += by.Ticks;

        // Never more than a window of it: a page that was hidden for a minute is not owed a minute of readings,
        // and the readings it is not owed are ones the window would have thrown away anyway. What is let go is a
        // gap in the recording, and a gap is drawn as a gap - a reading that is not a number is what a line breaks
        // a run on - rather than as a line straight across it.
        var window = (long)(Window * TimeSpan.TicksPerSecond);

        if (_owed > window)
        {
            _ticks += ((_owed - window) / step) * step;
            _owed = window;
            _due.Add(new FeedSample(At, double.NaN));
        }

        while (_owed >= step)
        {
            _owed -= step;
            _ticks += step;
            Arrived++;
            _due.Add(new FeedSample(At, _signal(_ticks / (double)TimeSpan.TicksPerSecond)));
        }

        return _due;
    }
}
