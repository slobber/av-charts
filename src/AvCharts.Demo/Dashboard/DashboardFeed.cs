using Avalonia.Threading;

namespace AvCharts.Demo.Dashboard;

/// <summary>The source of the dashboard's simulated shop-floor data.</summary>
public interface IDashboardFeed
{
    /// <summary>Raised ten times a second with the live point values and KPIs.</summary>
    event EventHandler<DashboardTick>? Ticked;

    /// <summary>Raised once a second with the two chart windows.</summary>
    event EventHandler<DashboardChartTick>? ChartTicked;

    /// <summary>Raised when a new alarm or event arrives.</summary>
    event EventHandler<AlarmEvent>? AlarmRaised;

    /// <summary>Resets the simulation and returns a fresh, deterministic snapshot.</summary>
    /// <param name="range">The chart window to build.</param>
    /// <returns>The snapshot.</returns>
    DashboardSnapshot Reset(DashboardRange range);

    /// <summary>Starts the timers, if they are not already running.</summary>
    void Start();

    /// <summary>Stops the timers.</summary>
    void Stop();
}

/// <summary>A deterministic simulated feed for the operations dashboard.</summary>
/// <remarks>
/// The values are made up, but the shape is the real one: a fast point feed, a slower aggregate, and an
/// event stream whose rate is not tied to either. The seed is fixed so that the first frame of the gallery
/// can be compared with a golden image; the clock is <see cref="DemoClock"/> so that a test can pin the
/// alarm timestamps too.
/// </remarks>
public sealed class DashboardFeed : IDashboardFeed
{
    private const int Seed = 20261017;

    private readonly PointSpec[] _specs =
    [
        new("Temperature", "°C", "0.0", 72.4, 8, 60, 80, 55, 88),
        new("Pressure", "bar", "0.00", 1.82, 0.18, 1.4, 2.2, 1.2, 2.4),
        new("Flow", "L/min", "0.0", 42.6, 6, 35, 50, 30, 55),
        new("Vibration", "mm/s", "0.00", 0.42, 0.16, 0, 0.6, 0, 1.0),
        new("Temperature", "°C", "0.0", 68.9, 7, 60, 80, 55, 88),
        new("Pressure", "bar", "0.00", 2.05, 0.16, 1.4, 2.2, 1.2, 2.4),
        new("Motor current", "A", "0.0", 12.6, 1.6, 8, 14, 6, 16),
        new("Conveyor speed", "rpm", "N0", 1240, 90, 1100, 1300, 1000, 1400),
    ];

    private readonly DispatcherTimer _pointTimer;
    private readonly DispatcherTimer _chartTimer;
    private readonly DispatcherTimer _alarmTimer;
    private Random _random = new(Seed);
    private readonly List<ThroughputPoint> _throughput = [];
    private readonly List<OeePoint> _oee = [];
    private readonly double[] _values = new double[8];
    private DashboardRange _range = DashboardRange.Last7Days;
    private DateTimeOffset _liveAt;
    private bool _started;

    /// <summary>Creates a feed, ready for a first <see cref="Reset"/>.</summary>
    public DashboardFeed()
    {
        _pointTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _chartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _alarmTimer = new DispatcherTimer();

        _pointTimer.Tick += (_, _) => TickPoints();
        _chartTimer.Tick += (_, _) => TickCharts();
        _alarmTimer.Tick += (_, _) => RaiseAlarm();
    }

    /// <inheritdoc />
    public event EventHandler<DashboardTick>? Ticked;

    /// <inheritdoc />
    public event EventHandler<DashboardChartTick>? ChartTicked;

    /// <inheritdoc />
    public event EventHandler<AlarmEvent>? AlarmRaised;

    /// <inheritdoc />
    public DashboardSnapshot Reset(DashboardRange range)
    {
        _range = range;
        _random = new Random(Seed);
        _throughput.Clear();
        _oee.Clear();

        BuildPoints();
        BuildCharts();

        var points = _specs
            .Select((spec, index) => new PointSample(spec.Name, _values[index], StatusOf(spec, _values[index])))
            .ToArray();

        var alarms = InitialAlarms();

        return new DashboardSnapshot(
            [.. _throughput],
            [.. _oee],
            points,
            alarms,
            1248,
            97.6,
            82.4);
    }

    /// <inheritdoc />
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _liveAt = DemoClock.Now().AddMilliseconds(500);
        _pointTimer.Start();
        _chartTimer.Start();
        ScheduleAlarm();
        _alarmTimer.Start();
    }

    /// <inheritdoc />
    public void Stop()
    {
        _started = false;
        _pointTimer.Stop();
        _chartTimer.Stop();
        _alarmTimer.Stop();
    }

    private void BuildPoints()
    {
        for (var index = 0; index < _specs.Length; index++)
        {
            _values[index] = _specs[index].Base + ((_random.NextDouble() - 0.5) * _specs[index].Amplitude * 0.35);
        }
    }

    private void BuildCharts()
    {
        var now = DemoClock.Now();
        var count = _range switch
        {
            DashboardRange.Last24Hours => 24,
            DashboardRange.Last7Days => 7,
            _ => 30,
        };
        var step = _range == DashboardRange.Last24Hours ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var last = _range == DashboardRange.Last24Hours
            ? new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset)
            : new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var first = last - (step * (count - 1));

        for (var index = 0; index < count; index++)
        {
            var when = first + (step * index);
            var received = 120 + (24 * Math.Sin(index / 1.7)) + (_random.NextDouble() * 18);
            var rejected = 7 + (3 * Math.Cos(index / 2.1)) + (_random.NextDouble() * 4);
            var oee = 82 + (4 * Math.Sin(index / 2.4)) + (_random.NextDouble() * 1.5);

            _throughput.Add(new ThroughputPoint(when, Math.Round(received, 1), Math.Round(rejected, 1)));
            _oee.Add(new OeePoint(when, Math.Round(oee, 1)));
        }
    }

    private AlarmEvent[] InitialAlarms()
    {
        var now = DemoClock.Now();

        return
        [
            new AlarmEvent(now.AddMinutes(-3), "Line A", AlarmSeverity.Critical, "Temperature above 80 °C"),
            new AlarmEvent(now.AddMinutes(-7), "Line B", AlarmSeverity.Warning, "Pressure below 1.4 bar"),
            new AlarmEvent(now.AddMinutes(-12), "Line A", AlarmSeverity.Warning, "Conveyor speed out of range"),
            new AlarmEvent(now.AddMinutes(-18), "Line B", AlarmSeverity.Warning, "Flow below 35 L/min"),
            new AlarmEvent(now.AddMinutes(-25), "Line A", AlarmSeverity.Info, "Operator acknowledged a warning"),
            new AlarmEvent(now.AddMinutes(-34), "Line B", AlarmSeverity.Info, "Shift started"),
        ];
    }

    private void TickPoints()
    {
        var now = DemoClock.Now();

        if (now < _liveAt)
        {
            return;
        }

        var seconds = (now - _liveAt).TotalSeconds;

        for (var index = 0; index < _specs.Length; index++)
        {
            var spec = _specs[index];
            var phase = (index * 0.8) + (seconds * 0.6);
            var noise = (_random.NextDouble() - 0.5) * spec.Amplitude * 0.12;
            _values[index] = spec.Base + (Math.Sin(phase) * spec.Amplitude) + noise;
        }

        var points = _specs
            .Select((spec, index) => new PointSample(spec.Name, _values[index], StatusOf(spec, _values[index])))
            .ToArray();

        // The rate is in units per hour, so the per-six-minute count is a tenth of it: a hundred and
        // twenty-four point eight readings a bucket is about the twelve hundred the first frame shows, rather
        // than a jump from one scale to another half a second after the page opens.
        var received = 124.8 + (Math.Sin(seconds * 0.5) * 22);
        var rejected = 3 + (Math.Cos(seconds * 0.4) * 1.2);
        var yield = 100 * received / (received + rejected);

        Ticked?.Invoke(this, new DashboardTick(
            points,
            Math.Round(received * 10, 0),
            Math.Round(yield, 1),
            Math.Round(82.4 + (Math.Sin(seconds * 0.3) * 1.2), 1)));
    }

    private void TickCharts()
    {
        if (!_started || _throughput.Count == 0)
        {
            return;
        }

        var last = _throughput[^1];
        var received = Math.Max(0, last.Received + ((_random.NextDouble() - 0.5) * 10));
        var rejected = Math.Max(0, last.Rejected + ((_random.NextDouble() - 0.5) * 3));
        _throughput[^1] = last with { Received = Math.Round(received, 1), Rejected = Math.Round(rejected, 1) };

        var lastOee = _oee[^1];
        _oee[^1] = lastOee with { Oee = Math.Round(Math.Clamp(lastOee.Oee + ((_random.NextDouble() - 0.5) * 2), 70, 100), 1) };

        ChartTicked?.Invoke(this, new DashboardChartTick([.. _throughput], [.. _oee]));
    }

    private void RaiseAlarm()
    {
        var roll = _random.Next(100);
        var severity = roll < 50 ? AlarmSeverity.Info : roll < 80 ? AlarmSeverity.Warning : AlarmSeverity.Critical;
        var line = _random.Next(2) == 0 ? "Line A" : "Line B";
        var message = severity switch
        {
            AlarmSeverity.Critical => _random.Next(2) == 0 ? "Temperature above 80 °C" : "Motor current above 16 A",
            AlarmSeverity.Warning => _random.Next(2) == 0 ? "Pressure below 1.4 bar" : "Vibration above 1.0 mm/s",
            _ => _random.Next(2) == 0 ? "Operator acknowledged a warning" : "Shift note recorded",
        };

        AlarmRaised?.Invoke(this, new AlarmEvent(DemoClock.Now(), line, severity, message));
        ScheduleAlarm();
    }

    private void ScheduleAlarm() =>
        _alarmTimer.Interval = TimeSpan.FromSeconds(4 + _random.NextDouble() * 5);

    private static PointStatus StatusOf(PointSpec spec, double value) =>
        value < spec.NormalMin || value > spec.NormalMax
            ? value < spec.AlarmMin || value > spec.AlarmMax
                ? PointStatus.Alarm
                : PointStatus.Warning
            : PointStatus.Normal;

    private sealed record PointSpec(
        string Name,
        string Unit,
        string Format,
        double Base,
        double Amplitude,
        double NormalMin,
        double NormalMax,
        double AlarmMin,
        double AlarmMax);
}
