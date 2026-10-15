using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
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
    /// A pen rather than a brush and a thickness, because that is what the token is. A border is a brush and a
    /// thickness, though, so the pen is taken apart once, here, and put on the box the template draws - the same
    /// way the data tip's is.
    /// </remarks>
    public static readonly StyledProperty<Pen?> BorderPenProperty =
        AvaloniaProperty.Register<Legend, Pen?>(nameof(BorderPen));

    /// <summary>Which side of the plot the legend is on.</summary>
    public static readonly StyledProperty<LegendPlacement> PlacementProperty =
        AvaloniaProperty.Register<Legend, LegendPlacement>(nameof(Placement), LegendPlacement.Right);

    private ChartBase? _watched;
    private Border? _surface;
    private WrapPanel? _lines;

    /// <summary>Creates a legend, which lists the series of the chart it is pointed at.</summary>
    public Legend()
    {
        // The panel the lines are laid out in: one wrap panel, whose direction follows the placement - across the
        // plot above and below it, down the plot beside it. It is built here rather than by a theme because a
        // theme's style arrives after the presenter has already built a panel of its own, and every line would
        // then keep the direction of whatever placement the legend happened to be created with.
        ItemsPanel = new FuncTemplate<Panel?>(() => _lines = new WrapPanel { Orientation = Down(Placement) });
        Place(Placement);
    }

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

    /// <summary>Which side of the plot the legend is on, which decides how its lines are laid out.</summary>
    /// <remarks>
    /// Where the legend sits in a page is the host's arrangement rather than the legend's, as it is in Flex: a
    /// legend is docked beside its chart, put in a cell of a grid, or left in a panel of its own. This is how the
    /// host says which of those it did, so that the lines run the right way - across the plot above and below it,
    /// down it beside it - and so that the room left between the two is on the side that faces the chart.
    /// </remarks>
    public LegendPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourceThemes.Apply(this, typeof(Legend));
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // The box the outline goes on: a theme of a host's own names the part the same way and gets the same
        // outline, and one with no such part keeps its own border.
        _surface = e.NameScope.Find<Border>("PART_Surface");
        Dress();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChartProperty)
        {
            Watch(Chart);
        }

        if (change.Property == BorderPenProperty)
        {
            Dress();
        }

        if (change.Property == PlacementProperty)
        {
            Place(Placement);
        }
    }

    /// <summary>Says which side it is on, so that a theme can say what that side looks like.</summary>
    /// <param name="placement">The side of the plot the legend is on.</param>
    /// <remarks>
    /// <para>
    /// The flow of the lines is the legend's own: above and below the plot a row is what a reader reads across,
    /// and beside it a column. It is set here rather than by a theme's style for the placement because the panel
    /// has to be in place before the lines are first laid out - a style that arrives with the theme arrives after
    /// the presenter has built a panel of its own, and the lines would keep the flow of the placement the legend
    /// was created with. A host that wants another flow sets <c>ItemsPanel</c> itself, which wins until the
    /// placement changes again.
    /// </para>
    /// <para>
    /// The room left between the legend and the chart is the theme's, by a pseudo-class per placement, the way
    /// being hidden is a pseudo-class: a gap is a thing a theme says, and a host that wants another one sets a
    /// margin of its own, which wins over a style.
    /// </para>
    /// </remarks>
    private void Place(LegendPlacement placement)
    {
        if (_lines is { } lines)
        {
            lines.Orientation = Down(placement);
        }

        Set(":top", placement == LegendPlacement.Top);
        Set(":bottom", placement == LegendPlacement.Bottom);
        Set(":left", placement == LegendPlacement.Left);
        Set(":right", placement == LegendPlacement.Right);
    }

    /// <summary>Which way the lines run for a placement: across the plot above and below it, down it beside it.</summary>
    /// <param name="placement">The side of the plot the legend is on.</param>
    /// <returns>The direction.</returns>
    private static Orientation Down(LegendPlacement placement) =>
        placement is LegendPlacement.Top or LegendPlacement.Bottom
            ? Orientation.Horizontal
            : Orientation.Vertical;

    /// <summary>Puts one of the placement's states on, or takes it off.</summary>
    /// <param name="state">The pseudo-class.</param>
    /// <param name="on">Whether the legend is on that side.</param>
    private void Set(string state, bool on)
    {
        if (on)
        {
            PseudoClasses.Add(state);
        }
        else
        {
            PseudoClasses.Remove(state);
        }
    }

    /// <summary>Puts the pen on the box the template drew, as the outline it is.</summary>
    private void Dress()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.BorderBrush = BorderPen?.Brush;
        _surface.BorderThickness = BorderPen is { Thickness: > 0 } pen ? new Thickness(pen.Thickness) : default;
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
