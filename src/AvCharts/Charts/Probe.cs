using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AvCharts.Charts;

/// <summary>
/// A stopwatch over the phases of a frame: the drawing code says what it is about to do, and this says what
/// doing it cost.
/// </summary>
/// <remarks>
/// <para>
/// Temporary. This is the instrument the browser performance line is measured with, and it is in the library
/// rather than beside the tests because the thing being measured runs in a browser as well as on a desktop: the
/// same marks are made on both sides, and only the clock and the collector differ.
/// </para>
/// <para>
/// <see cref="Mark"/> names the phase that is starting, and the time until the next mark is charged to it - so a
/// phase is measured by marking where it begins and letting the next mark close it. A name may carry a reading
/// after a <c>'#'</c>, which is reported beside the phase rather than as part of it: the number of readings a
/// frame sampled, for instance.
/// </para>
/// <para>
/// A mark is cheap and silent; every so many of them a line with the totals so far is written to the console,
/// which is how a browser gets them out of a page that has no way to call <see cref="Report"/> itself. The
/// report is cumulative, so the last line of a run is the whole of it.
/// </para>
/// </remarks>
public static class Probe
{
    /// <summary>How many marks go by before the totals are written to the console.</summary>
    private const int MarksPerLine = 32;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Cost> Totals = [];
    private static readonly List<string> Order = [];

    private static string? _phase;
    private static long _openedAt;
    private static int _sinceLine;

    /// <summary>Says a phase has started; the time until the next mark is what it cost.</summary>
    /// <param name="phase">The phase, optionally with a reading after a <c>'#'</c>.</param>
    public static void Mark(string phase)
    {
        ArgumentException.ThrowIfNullOrEmpty(phase);

        var hash = phase.IndexOf('#');
        var key = hash < 0 ? phase : phase[..hash];
        var reading = hash < 0 ? null : phase[(hash + 1)..];
        var now = Stopwatch.GetTimestamp();
        string? line = null;

        lock (Gate)
        {
            if (_phase is not null)
            {
                CostFor(_phase).Add((now - _openedAt) * 1000.0 / Stopwatch.Frequency);
            }

            // The reading belongs to the phase and not to the mark, so a phase that was marked with one keeps
            // showing the last one it saw rather than forgetting it the next time it is marked without one.
            if (reading is not null)
            {
                CostFor(key).Reading = reading;
            }

            _phase = key;
            _openedAt = now;

            if (++_sinceLine >= MarksPerLine)
            {
                _sinceLine = 0;
                line = Text();
            }
        }

        // Outside the lock: writing a line is the expensive part and nothing waits on it.
        if (line is not null)
        {
            Console.WriteLine(line);
        }
    }

    /// <summary>What every phase has cost so far, on one line.</summary>
    /// <returns>The totals, each as <c>phase=total/count/max</c> in milliseconds.</returns>
    public static string Report()
    {
        lock (Gate)
        {
            return Text();
        }
    }

    /// <summary>Throws away every total, and forgets the phase that was open.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            Totals.Clear();
            Order.Clear();
            _phase = null;
            _openedAt = 0;
            _sinceLine = 0;
        }
    }

    private static string Text()
    {
        var text = new StringBuilder("PROBE");
        var invariant = CultureInfo.InvariantCulture;

        foreach (var key in Order)
        {
            var cost = Totals[key];

            text.Append(' ').Append(key);

            if (cost.Reading is { Length: > 0 } reading)
            {
                text.Append('#').Append(reading);
            }

            text.Append('=')
                .Append(cost.Total.ToString("0.0", invariant))
                .Append('/')
                .Append(cost.Count.ToString(invariant))
                .Append('/')
                .Append(cost.Maximum.ToString("0.0", invariant));
        }

        return text.ToString();
    }

    private static Cost CostFor(string key)
    {
        if (!Totals.TryGetValue(key, out var cost))
        {
            cost = new Cost();
            Totals[key] = cost;
            Order.Add(key);
        }

        return cost;
    }

    /// <summary>What one phase cost, over however many times it ran.</summary>
    private sealed class Cost
    {
        public double Total { get; set; }

        public double Maximum { get; set; }

        public int Count { get; set; }

        public string? Reading { get; set; }

        public void Add(double milliseconds)
        {
            Total += milliseconds;
            Count++;

            if (milliseconds > Maximum)
            {
                Maximum = milliseconds;
            }
        }
    }
}
