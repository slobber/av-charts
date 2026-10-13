using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Themes;

namespace AvCharts.Controls;

/// <summary>
/// The list of what is on a chart: a line per series, saying what it is called, what colour it is drawn in, and
/// whether it is being shown. Clicking a line shows or hides that series.
/// </summary>
/// <remarks>
/// <para>
/// A control the host places itself, as it is in Flex, rather than something a chart grows inside itself: a
/// legend belongs somewhere in the page - beside the chart, under it, in a side panel - and only the page knows
/// where. Pointing it at a chart is the whole of the wiring.
/// </para>
/// <para>
/// Flex has no legend that hides anything: there, a series cannot even be hidden, so an application that wants
/// a legend that switches series on and off has to keep the list itself. Here the legend reads the chart's
/// series and switches their <see cref="Series.Visible"/>, which is the one property the rest of the library
/// already understands.
/// </para>
/// </remarks>
public class Legend : ItemsControl
{
    /// <summary>The chart whose series are being listed.</summary>
    public static readonly StyledProperty<ChartBase?> ChartProperty =
        AvaloniaProperty.Register<Legend, ChartBase?>(nameof(Chart));

    /// <summary>The pen drawn around the legend.</summary>
    /// <remarks>
    /// A pen rather than a brush and a thickness, because that is what the token is; the default template reads
    /// its brush and its thickness out of it, the same way the data tip's does.
    /// </remarks>
    public static readonly StyledProperty<Pen?> BorderPenProperty =
        AvaloniaProperty.Register<Legend, Pen?>(nameof(BorderPen));

    private ChartBase? _watched;

    /// <summary>The chart whose series are being listed; null, and the legend is empty.</summary>
    public ChartBase? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    /// <summary>The pen drawn around the legend.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.LegendBorder"/>.</remarks>
    public Pen? BorderPen
    {
        get => GetValue(BorderPenProperty);
        set => SetValue(BorderPenProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourceThemes.Apply(this, typeof(Legend));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChartProperty)
        {
            Watch(Chart);
        }
    }

    /// <summary>Starts listening to a chart, and stops listening to the one before it.</summary>
    /// <param name="chart">The chart to describe, or null to describe nothing.</param>
    private void Watch(ChartBase? chart)
    {
        if (ReferenceEquals(_watched, chart))
        {
            return;
        }

        if (_watched is { } was)
        {
            was.Series.CollectionChanged -= OnSeriesChanged;

            foreach (var series in was.Series)
            {
                series.VisibleChanged -= OnSeriesVisibleChanged;
                series.Invalidated -= OnSeriesInvalidated;
            }
        }

        _watched = chart;

        if (chart is { } now)
        {
            now.Series.CollectionChanged += OnSeriesChanged;

            foreach (var series in now.Series)
            {
                series.VisibleChanged += OnSeriesVisibleChanged;
                series.Invalidated += OnSeriesInvalidated;
            }
        }

        Rebuild();
    }

    /// <summary>Builds the lines again, one per series: the list itself changed.</summary>
    private void Rebuild()
    {
        Items.Clear();

        if (_watched is not { } chart)
        {
            return;
        }

        foreach (var series in chart.Series)
        {
            var item = new LegendItem { Series = series };
            item.Refresh();
            Items.Add(item);
        }
    }

    /// <summary>Reads every series again: a name or a colour may have changed under a legend already built.</summary>
    /// <remarks>
    /// Reading rather than rebuilding, and only ever assigning what actually changed: a series is invalidated on
    /// every frame's worth of pushed data, and a legend that rebuilt itself on each of those would be a legend
    /// that costs more than the chart.
    /// </remarks>
    private void Refresh()
    {
        foreach (var item in Items.OfType<LegendItem>())
        {
            item.Refresh();
        }
    }

    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_watched is not { } chart)
        {
            return;
        }

        // One handler for both ends of a list that just changed, because which series arrived and which left
        // decides only which subscriptions to move - and moving them all is cheaper to read than to work out.
        foreach (var series in chart.Series)
        {
            series.VisibleChanged -= OnSeriesVisibleChanged;
            series.VisibleChanged += OnSeriesVisibleChanged;
            series.Invalidated -= OnSeriesInvalidated;
            series.Invalidated += OnSeriesInvalidated;
        }

        Rebuild();
    }

    private void OnSeriesVisibleChanged(object? sender, EventArgs e) => Refresh();

    private void OnSeriesInvalidated(object? sender, EventArgs e) => Refresh();
}
