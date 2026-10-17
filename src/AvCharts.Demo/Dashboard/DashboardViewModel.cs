using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using AvCharts.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvCharts.Demo.Dashboard;

/// <summary>The ViewModel behind the operations dashboard.</summary>
/// <remarks>
/// The chart controls and their series live in the view's markup; this owns the data those series read, the
/// KPI text, the two tables, and the commands that change them. The feed is injected so that a test can
/// advance the dashboard by hand rather than wait a tenth of a second for a timer.
/// </remarks>
public sealed partial class DashboardViewModel : ObservableObject
{
    private const int AlarmCapacity = 20;

    private static readonly (string Name, string Unit, string Format)[] PointDefinitions =
    [
        ("Temperature", "°C", "0.0"),
        ("Pressure", "bar", "0.00"),
        ("Flow", "L/min", "0.0"),
        ("Vibration", "mm/s", "0.00"),
        ("Temperature", "°C", "0.0"),
        ("Pressure", "bar", "0.00"),
        ("Motor current", "A", "0.0"),
        ("Conveyor speed", "rpm", "N0"),
    ];

    private readonly IDashboardFeed _feed;
    private readonly Dictionary<string, PointReading> _pointByName = new(StringComparer.Ordinal);
    private bool _active;
    private bool _loading;

    /// <summary>Creates a dashboard over a feed, with its deterministic first snapshot loaded.</summary>
    /// <param name="feed">The feed.</param>
    public DashboardViewModel(IDashboardFeed feed)
    {
        _feed = feed;

        LineAPoints =
        [
            Point(0),
            Point(1),
            Point(2),
            Point(3),
        ];
        LineBPoints =
        [
            Point(4),
            Point(5),
            Point(6),
            Point(7),
        ];

        AlarmLevels =
        [
            new(AlarmSeverity.Info),
            new(AlarmSeverity.Warning),
            new(AlarmSeverity.Critical),
        ];

        Ranges =
        [
            new(DashboardRange.Last24Hours, Text.Say("Last 24h")),
            new(DashboardRange.Last7Days, Text.Say("7d")),
            new(DashboardRange.Last30Days, Text.Say("30d")),
        ];

        _selectedRange = Ranges[1];

        _feed.Ticked += OnTicked;
        _feed.ChartTicked += OnChartTicked;
        _feed.AlarmRaised += OnAlarmRaised;

        Load(_selectedRange.Range);
    }

    /// <summary>The throughput buckets the stacked column series reads.</summary>
    public ObservableCollection<ThroughputPoint> Throughput { get; } = [];

    /// <summary>The OEE buckets the line series reads.</summary>
    public ObservableCollection<OeePoint> OeePoints { get; } = [];

    /// <summary>The points in the left-hand table group.</summary>
    public ObservableCollection<PointReading> LineAPoints { get; }

    /// <summary>The points in the right-hand table group.</summary>
    public ObservableCollection<PointReading> LineBPoints { get; }

    /// <summary>The retained event log, newest first.</summary>
    public ObservableCollection<AlarmEvent> Alarms { get; } = [];

    /// <summary>The events visible under the current level filter, newest first.</summary>
    public ObservableCollection<AlarmEvent> VisibleAlarms { get; } = [];

    /// <summary>The three wedges of the active-alarm pie.</summary>
    public ObservableCollection<AlarmLevelCount> AlarmLevels { get; }

    /// <summary>The range options shown in the filter bar.</summary>
    public IReadOnlyList<DashboardRangeOption> Ranges { get; }

    /// <summary>The selected chart range.</summary>
    [ObservableProperty]
    private DashboardRangeOption _selectedRange;

    /// <summary>Whether the live feed is paused.</summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>The level the alarm table is filtered to, or null for every level.</summary>
    [ObservableProperty]
    private AlarmSeverity? _selectedSeverity;

    /// <summary>Throughput in units per hour.</summary>
    [ObservableProperty]
    private double _throughputRate;

    /// <summary>Yield in per cent.</summary>
    [ObservableProperty]
    private double _yield;

    /// <summary>OEE in per cent.</summary>
    [ObservableProperty]
    private double _oee;

    /// <summary>The number of alarms still active.</summary>
    [ObservableProperty]
    private int _activeAlarms;

    /// <summary>Throughput formatted for its KPI card.</summary>
    public string ThroughputText => $"{ThroughputRate.ToString("N0", CultureInfo.InvariantCulture)} u/h";

    /// <summary>Yield formatted for its KPI card.</summary>
    public string YieldText => $"{Yield.ToString("0.0", CultureInfo.InvariantCulture)} %";

    /// <summary>OEE formatted for its KPI card.</summary>
    public string OeeText => $"{Oee.ToString("0.0", CultureInfo.InvariantCulture)} %";

    /// <summary>The active alarm count formatted for its KPI card.</summary>
    public string ActiveAlarmText => ActiveAlarms.ToString(CultureInfo.InvariantCulture);

    /// <summary>The throughput series' name in the language being spoken.</summary>
    public string ReceivedLabel => Text.Say("Received");

    /// <summary>The rejected series' name in the language being spoken.</summary>
    public string RejectedLabel => Text.Say("Rejected");

    /// <summary>The OEE series' name in the language being spoken.</summary>
    public string OeeLabel => Text.Say("OEE");

    /// <summary>The KPI's change against the previous shift.</summary>
    public string ThroughputDeltaText => "+2.4%";

    /// <summary>The yield KPI's change against the previous shift.</summary>
    public string YieldDeltaText => "+0.8%";

    /// <summary>The OEE KPI's change against the previous shift.</summary>
    public string OeeDeltaText => "-0.6%";

    /// <summary>The active-alarm KPI's change against the previous shift.</summary>
    public string AlarmDeltaText => "+3";

    /// <summary>The button's words: what pressing it will do.</summary>
    public string PauseText => Text.Say(IsPaused ? "Resume" : "Pause");

    /// <summary>Whether the feed is live, in words.</summary>
    public string LiveText => Text.Say(IsPaused ? "Paused" : "Live");

    /// <summary>A dot that says whether the feed is live.</summary>
    public IBrush LiveBrush => IsPaused ? Brushes.Goldenrod : Brushes.LimeGreen;

    /// <summary>Starts the feed when the page is shown, unless the reader paused it.</summary>
    public void StartFeed()
    {
        _active = true;

        if (!IsPaused)
        {
            _feed.Start();
        }
    }

    /// <summary>Stops the feed when the page goes away.</summary>
    public void StopFeed()
    {
        _active = false;
        _feed.Stop();
    }

    /// <summary>Pauses or resumes the feed.</summary>
    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;

        if (!_active)
        {
            return;
        }

        if (IsPaused)
        {
            _feed.Stop();
        }
        else
        {
            _feed.Start();
        }
    }

    /// <summary>Reloads the current range and the six initial alarms.</summary>
    [RelayCommand]
    private void Refresh() => Load(SelectedRange.Range);

    /// <summary>Acknowledges one alarm, updating its state, the KPI and the pie.</summary>
    /// <param name="alarm">The alarm.</param>
    [RelayCommand]
    private void Acknowledge(AlarmEvent? alarm)
    {
        if (alarm is null || !alarm.IsActive)
        {
            return;
        }

        alarm.State = AlarmState.Acknowledged;
        RefreshAlarmLevels();
    }

    /// <summary>Filters the alarm table by the level of the clicked pie wedge.</summary>
    /// <param name="args">The chart's item-click arguments.</param>
    [RelayCommand]
    private void FilterLevel(ChartItemEventArgs? args)
    {
        if (args?.Item is not AlarmLevelCount level)
        {
            return;
        }

        SelectedSeverity = SelectedSeverity == level.Severity ? null : level.Severity;
    }

    partial void OnSelectedRangeChanged(DashboardRangeOption value)
    {
        if (!_loading)
        {
            Load(value.Range);
        }
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(PauseText));
        OnPropertyChanged(nameof(LiveText));
        OnPropertyChanged(nameof(LiveBrush));
    }

    partial void OnSelectedSeverityChanged(AlarmSeverity? value) => RebuildVisibleAlarms();

    partial void OnThroughputRateChanged(double value) => OnPropertyChanged(nameof(ThroughputText));

    partial void OnYieldChanged(double value) => OnPropertyChanged(nameof(YieldText));

    partial void OnOeeChanged(double value) => OnPropertyChanged(nameof(OeeText));

    partial void OnActiveAlarmsChanged(int value) => OnPropertyChanged(nameof(ActiveAlarmText));

    private PointReading Point(int index)
    {
        var (name, unit, format) = PointDefinitions[index];
        var reading = new PointReading(name, unit, format, 0, PointStatus.Normal);

        _pointByName[$"{index}:{name}"] = reading;
        return reading;
    }

    private void Load(DashboardRange range)
    {
        _feed.Stop();

        _loading = true;
        try
        {
            var snapshot = _feed.Reset(range);
            Apply(snapshot);
            SelectedRange = Ranges.Single(option => option.Range == range);
        }
        finally
        {
            _loading = false;
        }

        if (_active && !IsPaused)
        {
            _feed.Start();
        }
    }

    private void Apply(DashboardSnapshot snapshot)
    {
        Replace(Throughput, snapshot.Throughput);
        Replace(OeePoints, snapshot.OeePoints);

        for (var index = 0; index < snapshot.Points.Count && index < PointDefinitions.Length; index++)
        {
            var sample = snapshot.Points[index];

            if (_pointByName.TryGetValue($"{index}:{sample.Name}", out var point))
            {
                point.Value = sample.Value;
                point.Status = sample.Status;
            }
        }

        Alarms.Clear();
        foreach (var alarm in snapshot.Alarms)
        {
            Alarms.Add(alarm);
        }

        ThroughputRate = snapshot.ThroughputRate;
        Yield = snapshot.Yield;
        Oee = snapshot.OeeRate;
        RefreshAlarmLevels();
        RebuildVisibleAlarms();
    }

    private void OnTicked(object? sender, DashboardTick e)
    {
        for (var index = 0; index < e.Points.Count && index < PointDefinitions.Length; index++)
        {
            var sample = e.Points[index];

            if (_pointByName.TryGetValue($"{index}:{sample.Name}", out var point))
            {
                point.Value = sample.Value;
                point.Status = sample.Status;
            }
        }

        ThroughputRate = e.ThroughputRate;
        Yield = e.Yield;
        Oee = e.Oee;
    }

    private void OnChartTicked(object? sender, DashboardChartTick e)
    {
        Replace(Throughput, e.Throughput);
        Replace(OeePoints, e.OeePoints);
    }

    private void OnAlarmRaised(object? sender, AlarmEvent alarm)
    {
        Alarms.Insert(0, alarm);

        AlarmEvent? dropped = null;

        while (Alarms.Count > AlarmCapacity)
        {
            dropped = Alarms[^1];
            Alarms.RemoveAt(Alarms.Count - 1);
        }

        // The newest row is inserted into the view rather than the view being rebuilt: a reader who scrolled
        // down the log keeps their place, which is what "no forced auto-scroll" means in practice.
        if (SelectedSeverity is null || SelectedSeverity == alarm.Severity)
        {
            VisibleAlarms.Insert(0, alarm);
        }

        if (dropped is not null)
        {
            VisibleAlarms.Remove(dropped);
        }

        RefreshAlarmLevels();
    }

    private void RefreshAlarmLevels()
    {
        foreach (var level in AlarmLevels)
        {
            level.Count = Alarms.Count(alarm => alarm.IsActive && alarm.Severity == level.Severity);
        }

        ActiveAlarms = AlarmLevels.Sum(level => level.Count);
    }

    private void RebuildVisibleAlarms()
    {
        VisibleAlarms.Clear();

        foreach (var alarm in Alarms)
        {
            if (SelectedSeverity is null || alarm.Severity == SelectedSeverity)
            {
                VisibleAlarms.Add(alarm);
            }
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
