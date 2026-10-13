using AvCharts.Charts;

namespace AvCharts.Controls;

/// <summary>
/// What a chart says when something happened to a record: which record it was, and what is selected now.
/// </summary>
/// <remarks>
/// <para>
/// A data point is not a thing on screen - there is no visual behind it and nothing to subscribe to - so every
/// per-record event a chart raises is one the chart made up itself, out of a hit test, as the pointer or the
/// keyboard moved. That is worth knowing before asking why a handler for a record that was never clicked ran, or
/// why there is no sender to match a record to.
/// </para>
/// <para>
/// The selection is the chart's own list rather than a copy, so a handler that keeps it keeps a live view of
/// what is selected; copy it if it has to stay as it was.
/// </para>
/// </remarks>
public sealed class ChartItemEventArgs : EventArgs
{
    /// <summary>Creates the arguments for an event about the selection, or about one record, or both.</summary>
    /// <param name="selection">What is selected, as the chart has it.</param>
    /// <param name="hit">The record the event is about, when it is about one.</param>
    public ChartItemEventArgs(IReadOnlyList<ChartItem> selection, HitData? hit = null)
    {
        Selection = selection;
        Hit = hit;
    }

    /// <summary>What is selected, as the chart has it: the chart's own list rather than a copy.</summary>
    public IReadOnlyList<ChartItem> Selection { get; }

    /// <summary>The record the event is about, with everything a hit test found out about it.</summary>
    public HitData? Hit { get; }

    /// <summary>The record the event is about, or null when it is about the selection as a whole.</summary>
    public ChartItem? ChartItem => Hit?.ChartItem;

    /// <summary>The caller's own object behind that record, when the series can still find it.</summary>
    public object? Item => Hit?.Item;
}
