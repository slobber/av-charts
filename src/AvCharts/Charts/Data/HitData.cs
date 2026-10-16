using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// What a hit test found: a record of a series, how far the pointer was from it, and what to say about it.
/// </summary>
/// <remarks>
/// A data point is not a thing on screen - there is no visual behind it and nothing to be clicked - so this is
/// made up by the chart when it is asked what is under the pointer. That is also why everything a caller needs
/// is here rather than on the item: the series it belongs to, the record, the caller's own object, the colour it
/// is drawn in and a line of text for a tip.
/// </remarks>
public readonly record struct HitData
{
    /// <summary>How far the pointer was from the point, in logical pixels.</summary>
    public required double Distance { get; init; }

    /// <summary>The record that was hit.</summary>
    public required ChartItem ChartItem { get; init; }

    /// <summary>The text a data tip would show for this point.</summary>
    public required string DisplayText { get; init; }

    /// <summary>The series the point belongs to.</summary>
    public Series Element => ChartItem.Series;

    /// <summary>The caller's object, when the series can still find it.</summary>
    public object? Item => ChartItem.Item;

    /// <summary>The horizontal value, in data space.</summary>
    public ChartValue X => ChartItem.X;

    /// <summary>The vertical value, in data space.</summary>
    public ChartValue Y => ChartItem.Y;

    /// <summary>The colour the series is drawn in, which is what a tip or a legend shows beside it.</summary>
    public IBrush? SeriesColor => ChartItem.Series.Color;

    /// <summary>The position of the point on the chart, when it has one.</summary>
    public Avalonia.Point? Position => ChartItem.Position;
}
