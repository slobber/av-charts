using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Demo;
using AvCharts.Series;
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
/// It walks the pages a reader is shown rather than building charts of its own: the combination chart is on
/// Columns, the second scale on Two scales, the long recording on Scrolling, and the dashboard's own frame on
/// Dashboard.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class AcceptanceTests
{
    [Fact]
    public void A_column_chart_with_a_line_over_it()
    {
        var page = Show("Columns");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        // The page's own helper makes the markup series, which is a named subclass rather than the generic type.
        var chart = page.GetVisualDescendants().OfType<CartesianChart>()
            .First(one => one.Series.Any(series =>
                series.GetType().Name.StartsWith("LineSeries", StringComparison.Ordinal)));
        var kinds = chart.Series.Select(series => series.GetType().Name).ToArray();

        Assert.Contains(kinds, kind => kind.StartsWith("ColumnSeries", StringComparison.Ordinal));
        Assert.Contains(kinds, kind => kind.StartsWith("LineSeries", StringComparison.Ordinal));

        // And both are painted: a combination chart that drew one half of itself would pass everything else.
        var bitmap = Picture(chart);

        Assert.True(Ink.Count(bitmap) > 0);
    }

    [Fact]
    public void Two_vertical_scales_on_one_plot()
    {
        var (chart, _) = Page("Two scales");
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
        var (chart, _) = Page("Dashboard");

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
        var (chart, _) = Page("Dashboard");

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
    public void The_columns_of_a_date_axis_are_drawn_and_can_be_arranged()
    {
        // "Columns stacked under a line" is what this page says it shows, and for a long time it showed only the
        // line: a column stood in one unit of the band axis, and a unit of a date axis is a millisecond, so two
        // days' readings came out a thousandth of a pixel wide - which is to say not at all. The knob that
        // arranges the columns had nothing to arrange, which is the shape this test is here to keep out.
        var page = Page("Dashboard").Page;
        var chart = page.GetVisualDescendants().OfType<CartesianChart>().First();
        var columns = chart.Series[0];

        Assert.Equal(2, chart.Series.Count);

        chart.SeriesLayout = SeriesLayout.Cluster;
        Lay(chart);
        var clustered = ColumnsOnly(chart, columns);

        Assert.True(Ink.Count(clustered) > 0, "the column series drew nothing at all on a date axis");

        chart.SeriesLayout = SeriesLayout.Stacked;
        Lay(chart);
        var stacked = ColumnsOnly(chart, columns);

        Assert.True(
            Ink.Differences(clustered, stacked) > 0,
            "side by side and stacked draw the same picture, so the knob has nothing to arrange");
    }

    /// <summary>Paints one series on its own, so that what it drew is what is being measured.</summary>
    /// <param name="chart">The chart, laid out.</param>
    /// <param name="series">The series.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap ColumnsOnly(CartesianChart chart, SeriesBase series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(640, 720));

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }

    /// <summary>Lays a chart out again, which a change of arrangement wants and a test has to ask for.</summary>
    private static void Lay(CartesianChart chart)
    {
        var size = new Size(chart.Bounds.Width, chart.Bounds.Height);

        chart.Measure(size);
        chart.Arrange(new Rect(size));
    }

    [Fact]
    public void A_hundred_thousand_points()
    {
        var page = Page("Scrolling").Page;
        var big = HundredThousand(page);
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
    public void A_hundred_thousand_points_sampled_puts_the_peaks_where_the_readings_do()
    {
        // The window shows the first half of the readings, so the run reaches twice as far as the plot does. The
        // columns the series thins into are the plot's, and a column is two pixels wide however far the run
        // reaches - otherwise a peak is drawn as the chord across it, and every crest sits a few pixels left of
        // where the readings put it, which is what "sampled to the pixels there are" is supposed to rule out.
        var page = Page("Scrolling").Page;
        var big = HundredThousand(page);
        var series = (LineSeries<double>)big.Series[0];
        var colour = ((ISolidColorBrush)series.Color!).Color;
        var sampled = Picture(big);

        // The same window with nothing thinned: what the outline is supposed to look like.
        series.Sampling = SamplingMode.None;
        var exact = Picture(big);
        var worst = 0;

        for (var column = 0; column < sampled.PixelSize.Width; column++)
        {
            var (topSampled, bottomSampled) = Ink.Column(sampled, column, colour);
            var (topExact, bottomExact) = Ink.Column(exact, column, colour);

            if (topSampled < 0 || topExact < 0)
            {
                continue;
            }

            worst = Math.Max(worst, Math.Abs(topSampled - topExact));
            worst = Math.Max(worst, Math.Abs(bottomSampled - bottomExact));
        }

        // One pixel, and that is the arithmetic rather than a hope: a column is a pixel wide, the curve's period
        // is about thirty of them at this size, so the chord across a column sags by about half a pixel. Two
        // pixels to a column - which is what kept two points to two columns before this - measured 2.
        Assert.True(worst <= 1, $"the sampled curve leaves the readings by {worst} pixels in one column");
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

        // The two charts of the comparison, and the playground under them: whichever of the three is a knob, the
        // first two are the ones this is about.
        Assert.True(charts.Length >= 2, "the palette page should have its comparison on it");

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
        var (chart, _) = Page("Dashboard");

        // Tips, but not one per reading: this page is a chart to read with a pointer, so nothing is written out
        // until there is a pointer to read with. What a tip then says and where it stands is DataTipTests'.
        Assert.False(chart.ShowAllDataTips);
        Assert.Equal(DataTipMode.Item, chart.DataTipMode);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<DataTip>(), tip => tip.IsVisible);

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

    /// <summary>The chart on a page carrying a hundred thousand readings.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart HundredThousand(Control page) =>
        page.GetVisualDescendants()
            .OfType<CartesianChart>()
            .Single(chart => chart.Series.OfType<LineSeries<double>>()
                .Any(series => series.ItemsSource?.Count() == 100_000));
}
