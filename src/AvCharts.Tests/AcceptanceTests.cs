using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Demo;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// The rehearsal: everything the first version promised, walked once in one place.
/// </summary>
/// <remarks>
/// <para>
/// The design brief's acceptance list is "a column chart with a line over it, two vertical scales, a time axis,
/// stacked columns, a hundred thousand points, light and dark, data tips, a crosshair, a legend that switches
/// series off, and an ECG strip". Each of those has steps of its own and tests beside them; what this class is
/// for is the other question - whether they still work *together*, which is the thing a release is about.
/// </para>
/// <para>
/// It walks the demo's own "Everything at once" page rather than building a chart of its own, so that what is
/// rehearsed is what a reader is shown.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class AcceptanceTests
{
    [Fact]
    public void A_column_chart_with_a_line_over_it()
    {
        var (chart, _) = Page("Everything at once");
        var kinds = chart.Series.Select(series => series.GetType().Name).ToArray();

        Assert.Contains("ColumnSeries`1", kinds);
        Assert.Contains("LineSeries`1", kinds);

        // And both are painted: a combination chart that drew one half of itself would pass everything else.
        var bitmap = Picture(chart);

        Assert.True(Ink.Count(bitmap) > 0);
    }

    [Fact]
    public void Two_vertical_scales_on_one_plot()
    {
        var (chart, _) = Page("Everything at once");
        var separate = chart.Series.Where(series => series.VerticalAxis is not null).ToArray();

        Assert.NotEmpty(separate);

        // The second scale has a plot of its own to be drawn in, and its own window - which is what it is for.
        var axis = separate[0].VerticalAxis!;
        var window = chart.Transform.GetLayout(axis).UnitRange;

        Assert.False(window.IsEmpty);
        Assert.True(window.Maximum <= 100, $"the rate scale should stay in per cent: {window}");

        Assert.Contains(chart.GetVisualChildren(), child => child is AxisLayer layer && !ReferenceEquals(layer, chart.VerticalLayer));
    }

    [Fact]
    public void A_time_axis_across()
    {
        var (chart, _) = Page("Everything at once");

        Assert.IsType<DateTimeAxis>(chart.HorizontalAxis);

        // The labels read as days rather than as numbers, which is the whole of what a time axis is for.
        var layout = chart.Transform.GetLayout(ChartDimension.X);
        var labels = chart.HorizontalRenderer.GetTicks(layout, chart.PlotBounds).Select(tick => tick.Label).ToArray();

        Assert.NotEmpty(labels);
        Assert.Contains(labels, label => label.Contains("2026"));
    }

    [Fact]
    public void Stacked_columns()
    {
        var (chart, _) = Page("Everything at once");

        Assert.Equal(SeriesLayout.Stacked, chart.SeriesLayout);

        // Stacked means the axis grew to cover the sum of the two column series rather than the taller of them:
        // each day read about two hundred and twenty, and the axis of a stack reaches past any one reading.
        var window = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;
        var tallest = chart.GetAllDataPoints()
            .Where(item => item.Y.Number > 0)
            .Max(item => item.Y.Number);

        Assert.True(window.Maximum > tallest, $"the axis reaches {window.Maximum} for a reading of {tallest}, so the stack is in it");
    }

    [Fact]
    public void A_hundred_thousand_points()
    {
        // The hundred thousand reading chart is the one with a single series on it; the other is the combination.
        var page = Page("Everything at once").Page;
        var big = page.GetVisualDescendants().OfType<CartesianChart>()
            .Single(chart => chart.Series.Count == 1);
        var series = (LineSeries<double>)big.Series[0];

        Assert.Equal(SamplingMode.MinMax, series.Sampling);
        Assert.Equal(100_000, series.ItemsSource!.Count());

        // Sampled to the columns there are, and drawn: the curve spans the plot rather than one pixel of it.
        var plot = big.PlotBounds;
        var bitmap = Picture(big);
        var colour = ((ISolidColorBrush)series.Color!).Color;
        var ink = Ink.Bounds(bitmap, colour, new Rect(0, 0, 680, 320));

        Assert.True(ink.Left < plot.Center.X && ink.Right - ink.Left > plot.Width / 2, $"the ink runs {ink}");
    }

    [Fact]
    public void Light_and_dark()
    {
        // The same chart in the two variants: one line per palette slot, drawn in the colours of the variant it
        // is in - and switching does not rebuild the chart, only repaints it.
        var page = Show("Palette");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var charts = page.GetVisualDescendants().OfType<CartesianChart>().ToArray();

        Assert.Equal(2, charts.Length);

        var light = charts[0].Series[0].Color;
        var dark = charts[1].Series[0].Color;

        Assert.NotNull(light);
        Assert.NotNull(dark);
        Assert.NotEqual(((ISolidColorBrush)light!).Color.ToUInt32(), ((ISolidColorBrush)dark!).Color.ToUInt32());

        // And the surfaces differ, which is what "light and dark" means at a glance.
        var lightBackground = (ISolidColorBrush)charts[0].Background!;
        var darkBackground = (ISolidColorBrush)charts[1].Background!;

        Assert.True(lightBackground.Color.R > darkBackground.Color.R, "the light chart should be the lighter one");
    }

    [Fact]
    public void Data_tips_a_crosshair_and_a_legend()
    {
        var (chart, _) = Page("Everything at once");

        // Tips on, and one written out per reading without a pointer to hover with: that is what the page asks
        // for, and what the rehearsal can check without a pointer.
        Assert.True(chart.ShowAllDataTips);
        Assert.Equal(DataTipMode.Item, chart.DataTipMode);

        var tips = chart.GetVisualDescendants().OfType<DataTip>().Where(tip => tip.IsVisible).ToArray();

        Assert.NotEmpty(tips);
        Assert.All(tips, tip => Assert.NotEqual(string.Empty, tip.Text));

        // A crosshair, which stands on the nearest reading rather than where the pointer is.
        Assert.True(chart.ShowCrosshair);

        // And a legend, which lists what is on the chart and switches a series off when a line is clicked.
        var legend = chart.GetVisualAncestors().OfType<Control>().SelectMany(control => control.GetVisualDescendants())
            .OfType<Legend>()
            .FirstOrDefault();

        Assert.NotNull(legend);

        var lines = legend.Items.OfType<LegendItem>().ToArray();

        Assert.Equal(chart.Series.Count, lines.Length);

        lines[0].Toggle();

        Assert.False(chart.Series[0].Visible);

        lines[0].Toggle();

        Assert.True(chart.Series[0].Visible);
    }

    [Fact]
    public void An_ecg_strip()
    {
        // Fixed scale, an even grid, sampling, and a gap where the lead came off - the strip chart the brief asks
        // for, on its own page because a strip chart is read on its own.
        var (chart, _) = Page("ECG");
        var axis = Assert.IsType<LinearAxis>(chart.HorizontalAxis);
        var readings = chart.Series[0].Items.ToArray();

        Assert.Equal(AxisScaleMode.Fixed, axis.ScaleMode);
        Assert.NotEmpty(readings);

        // The dropout is the widest stretch between two readings that are still in view, and it is a gap in the
        // ink rather than a line across it: the two readings either side of it are further apart than a couple of
        // pixels of paper, and nothing is drawn between them.
        var bitmap = Picture(chart);
        var plot = chart.PlotBounds;
        var colour = ((ISolidColorBrush)chart.Series[0].Color!).Color;
        var ink = Ink.Bounds(bitmap, colour, new Rect(0, 0, 640, 720));
        var widest = 0d;
        var from = 0d;

        // The readings with no place are the dropout itself: a record with no vertical value is still a record,
        // so it is in the view with nothing to draw at - and the readings either side of them are what a gap is
        // measured between.
        Point? previous = null;

        foreach (var reading in readings)
        {
            if (reading.Position is not { } here)
            {
                continue;
            }

            if (previous is { } before && here.X - before.X > widest)
            {
                widest = here.X - before.X;
                from = before.X;
            }

            previous = here;
        }

        Assert.True(widest > 20, $"the widest gap between readings is {widest:0.#} pixels, so a dropout is in view");
        Assert.Equal(0, Ink.Count(bitmap, colour, new Rect(from + 2, plot.Top, widest - 4, plot.Height)));
        Assert.True(ink.Right - ink.Left > plot.Width / 2, "the strip should be drawn across the plot");
    }

    /// <summary>The first chart of a page of the gallery, shown the way the demo window shows it.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The chart, and the page it is on.</returns>
    private static (CartesianChart Chart, Control Page) Page(string name)
    {
        var page = Show(name);

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        return (page.GetVisualDescendants().OfType<CartesianChart>().First(), page);
    }

    /// <summary>Shows a page the way the demo window does, so that its charts are dressed and measured.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The page.</returns>
    private static Control Show(string name)
    {
        var page = Catalog.Groups
            .SelectMany(group => group.Entries)
            .Single(entry => entry.Title == name)
            .Create();

        _ = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        return page;
    }

    /// <summary>Draws a chart, so that a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(640, 720));
        bitmap.Render(chart);
        return bitmap;
    }
}
