using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvCharts.Demo.Dashboard;

/// <summary>The window of history the two dashboard charts show.</summary>
public enum DashboardRange
{
    Last24Hours,
    Last7Days,
    Last30Days,
}

/// <summary>How serious an alarm is.</summary>
public enum AlarmSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>Whether an alarm still needs somebody to look at it.</summary>
public enum AlarmState
{
    Active,
    Acknowledged,
}

/// <summary>How a point's current value compares with its limits.</summary>
public enum PointStatus
{
    Normal,
    Warning,
    Alarm,
}

/// <summary>One range option in the dashboard's filter bar.</summary>
/// <param name="Range">The range.</param>
/// <param name="Label">The words the filter shows, in the language being spoken.</param>
public sealed record DashboardRangeOption(DashboardRange Range, string Label);

/// <summary>One item in the alarm/event table.</summary>
public sealed partial class AlarmEvent : ObservableObject
{
    /// <summary>Creates an alarm or event.</summary>
    /// <param name="when">When it happened.</param>
    /// <param name="line">The line or device it belongs to.</param>
    /// <param name="severity">How serious it is.</param>
    /// <param name="message">What happened.</param>
    /// <param name="state">Whether it still needs attention.</param>
    public AlarmEvent(
        DateTimeOffset when,
        string line,
        AlarmSeverity severity,
        string message,
        AlarmState state = AlarmState.Active)
    {
        When = when;
        Line = line;
        Severity = severity;
        Message = message;
        State = state;
    }

    /// <summary>When it happened.</summary>
    public DateTimeOffset When { get; }

    /// <summary>The line or device it belongs to.</summary>
    public string Line { get; }

    /// <summary>How serious it is.</summary>
    public AlarmSeverity Severity { get; }

    /// <summary>What happened.</summary>
    public string Message { get; }

    /// <summary>Whether it still needs attention.</summary>
    [ObservableProperty]
    private AlarmState _state;

    /// <summary>The time as the table writes it.</summary>
    public string TimeText => When.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>The line's words in the language being spoken.</summary>
    public string LineText => Text.Say(Line);

    /// <summary>The severity's words in the language being spoken.</summary>
    public string SeverityText => Text.Say(Severity.ToString());

    /// <summary>The message in the language being spoken.</summary>
    public string MessageText => Text.Say(Message);

    /// <summary>The state's words in the language being spoken.</summary>
    public string StateText => Text.Say(State.ToString());

    /// <summary>Whether the alarm still needs attention.</summary>
    public bool IsActive => State == AlarmState.Active;

    /// <summary>Whether the alarm is critical, for the table's colour.</summary>
    public bool IsCritical => Severity == AlarmSeverity.Critical;

    /// <summary>Whether the alarm is a warning, for the table's colour.</summary>
    public bool IsWarning => Severity == AlarmSeverity.Warning;

    /// <summary>The colour the table writes this severity in.</summary>
    public IBrush SeverityBrush => Severity switch
    {
        AlarmSeverity.Critical => Brush.Parse("#B42318"),
        AlarmSeverity.Warning => Brush.Parse("#B54708"),
        _ => Brush.Parse("#475467"),
    };

    /// <summary>Acknowledged rows are written more quietly than active ones.</summary>
    public double StateOpacity => IsActive ? 1 : 0.55;

    partial void OnStateChanged(AlarmState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(StateOpacity));
    }
}

/// <summary>One point in the live point table.</summary>
public sealed partial class PointReading : ObservableObject
{
    /// <summary>Creates a point reading.</summary>
    /// <param name="name">The point's English name, which is also its translation key.</param>
    /// <param name="unit">The unit written after the value.</param>
    /// <param name="format">The numeric format.</param>
    /// <param name="value">The current value.</param>
    /// <param name="status">The current status.</param>
    public PointReading(string name, string unit, string format, double value, PointStatus status)
    {
        Name = name;
        Unit = unit;
        Format = format;
        _value = value;
        _status = status;
    }

    /// <summary>The point's English name, which is also its translation key.</summary>
    public string Name { get; }

    /// <summary>The unit written after the value.</summary>
    public string Unit { get; }

    /// <summary>The numeric format.</summary>
    public string Format { get; }

    /// <summary>The current value.</summary>
    [ObservableProperty]
    private double _value;

    /// <summary>The current status.</summary>
    [ObservableProperty]
    private PointStatus _status;

    /// <summary>The point's words in the language being spoken.</summary>
    public string DisplayName => Text.Say(Name);

    /// <summary>The value with its unit, as the table writes it.</summary>
    public string ValueText => $"{Value.ToString(Format, CultureInfo.InvariantCulture)} {Unit}";

    /// <summary>The status's words in the language being spoken.</summary>
    public string StatusText => Text.Say(Status.ToString());

    /// <summary>Whether the point is in its warning band.</summary>
    public bool IsWarning => Status == PointStatus.Warning;

    /// <summary>Whether the point is in its alarm band.</summary>
    public bool IsAlarm => Status == PointStatus.Alarm;

    /// <summary>The colour the table writes this status in.</summary>
    public IBrush StatusBrush => Status switch
    {
        PointStatus.Alarm => Brush.Parse("#B42318"),
        PointStatus.Warning => Brush.Parse("#B54708"),
        _ => Brush.Parse("#067647"),
    };

    partial void OnValueChanged(double value) => OnPropertyChanged(nameof(ValueText));

    partial void OnStatusChanged(PointStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsAlarm));
        OnPropertyChanged(nameof(StatusBrush));
    }
}

/// <summary>One wedge of the active-alarm pie.</summary>
public sealed partial class AlarmLevelCount : ObservableObject
{
    /// <summary>Creates a wedge for one severity.</summary>
    /// <param name="severity">The severity.</param>
    public AlarmLevelCount(AlarmSeverity severity)
    {
        Severity = severity;
    }

    /// <summary>The severity.</summary>
    public AlarmSeverity Severity { get; }

    /// <summary>How many active alarms there are at this severity.</summary>
    [ObservableProperty]
    private int _count;

    /// <summary>The severity's words in the language being spoken.</summary>
    public string Label => Text.Say(Severity.ToString());

    /// <summary>The wedge's label: the severity and its count.</summary>
    public string LabelText => $"{Label} {Count}";

    partial void OnCountChanged(int value) => OnPropertyChanged(nameof(LabelText));
}

/// <summary>One point's value at one tick.</summary>
/// <param name="Name">The point's English name.</param>
/// <param name="Value">The value.</param>
/// <param name="Status">The status.</param>
public sealed record PointSample(string Name, double Value, PointStatus Status);

/// <summary>One bucket of the throughput chart.</summary>
/// <param name="When">The bucket's start.</param>
/// <param name="Received">How many readings were received.</param>
/// <param name="Rejected">How many were rejected.</param>
public sealed record ThroughputPoint(DateTimeOffset When, double Received, double Rejected);

/// <summary>One bucket of the OEE chart.</summary>
/// <param name="When">The bucket's start.</param>
/// <param name="Oee">The OEE percentage.</param>
public sealed record OeePoint(DateTimeOffset When, double Oee);

/// <summary>The 100 ms values the dashboard consumes.</summary>
/// <param name="Points">The point values.</param>
/// <param name="ThroughputRate">Throughput in units per hour.</param>
/// <param name="Yield">Yield in per cent.</param>
/// <param name="Oee">OEE in per cent.</param>
public sealed record DashboardTick(
    IReadOnlyList<PointSample> Points,
    double ThroughputRate,
    double Yield,
    double Oee);

/// <summary>The one-second aggregate the two charts consume.</summary>
/// <param name="Throughput">The throughput buckets.</param>
/// <param name="OeePoints">The OEE buckets.</param>
public sealed record DashboardChartTick(
    IReadOnlyList<ThroughputPoint> Throughput,
    IReadOnlyList<OeePoint> OeePoints);

/// <summary>The complete state a fresh dashboard starts from.</summary>
/// <param name="Throughput">The throughput buckets.</param>
/// <param name="OeePoints">The OEE buckets.</param>
/// <param name="Points">The point values.</param>
/// <param name="Alarms">The initial alarm events.</param>
/// <param name="ThroughputRate">Throughput in units per hour.</param>
/// <param name="Yield">Yield in per cent.</param>
/// <param name="OeeRate">OEE in per cent.</param>
public sealed record DashboardSnapshot(
    IReadOnlyList<ThroughputPoint> Throughput,
    IReadOnlyList<OeePoint> OeePoints,
    IReadOnlyList<PointSample> Points,
    IReadOnlyList<AlarmEvent> Alarms,
    double ThroughputRate,
    double Yield,
    double OeeRate);
