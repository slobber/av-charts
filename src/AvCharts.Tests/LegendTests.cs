using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the legend: that it lists the chart's series with the colours they are drawn in, that clicking a line
/// shows or hides that series, that the line says which it is, and that a host can dress the whole thing.
/// </summary>
/// <remarks>
/// The page here is a window with a legend beside a chart, which is how a host arranges one: the legend is a
/// control the host places, as it is in Flex, and pointing it at a chart is the whole of the wiring.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class LegendTests
{
    [Fact]
    public void The_legend_lists_the_series_with_the_colours_they_are_drawn_in()
    {
        var (legend, chart, _) = Show(Named("Depth"), Named("Speed"));

        var items = Lines(legend);

        Assert.Equal(["Depth", "Speed"], items.Select(item => item.Text!).ToArray());

        // The colours are the ones the chart drew them in - the palette's first two slots - rather than anything
        // the legend chose.
        foreach (var (item, series) in items.Zip(chart.Series))
        {
            Assert.Same(series.Color, item.Colour);
            Assert.NotNull(item.Colour);
            Assert.Same(series, item.Series);
        }

        // A series added later is a line later, without the host touching the legend again.
        chart.Series.Add(Named("Pressure"));

        Assert.Equal(["Depth", "Speed", "Pressure"], Lines(legend).Select(item => item.Text!).ToArray());

        // And a legend pointed at nothing lists nothing.
        legend.Chart = null;

        Assert.Empty(Lines(legend));
    }

    [Fact]
    public void Clicking_a_line_hides_its_series_and_the_line_says_so()
    {
        var (legend, chart, theme) = Show(Named("Depth"), Named("Speed"));
        var item = Lines(legend)[0];
        var series = chart.Series[0];

        Assert.False(item.IsOff);

        item.Toggle();

        // The series is hidden, which is the one property the rest of the library already understands - the axis
        // it stretched, the ink it painted and the records a hit test finds all go with it.
        Assert.False(series.Visible);
        Assert.True(item.IsOff);
        Assert.Contains(":off", item.Classes);
        Assert.Same(themeValue(theme, ChartTokens.LegendItemDisabled), item.Foreground);
        Assert.DoesNotContain(series, chart.GetAllDataPoints().Select(point => point.Series).Distinct());

        item.Toggle();

        Assert.True(series.Visible);
        Assert.False(item.IsOff);
        Assert.DoesNotContain(":off", item.Classes);
        Assert.Same(themeValue(theme, ChartTokens.LegendForeground), item.Foreground);
    }

    [Fact]
    public void A_series_hidden_elsewhere_shows_in_the_legend_as_hidden()
    {
        var (legend, chart, _) = Show(Named("Depth"), Named("Speed"));

        // Hiding is one property, so it does not matter who hides: the legend follows the series rather than
        // being the only way to change it.
        chart.Series[1].Visible = false;

        Assert.True(Lines(legend)[1].IsOff);
        Assert.False(Lines(legend)[0].IsOff);
    }

    [Fact]
    public void The_theme_dresses_the_legend_and_a_host_can_dress_it_differently()
    {
        var (legend, _, theme) = Show(Named("Depth"));

        Assert.Same(themeValue(theme, ChartTokens.LegendBackground), legend.Background);
        Assert.Same(themeValue(theme, ChartTokens.LegendBorder), legend.BorderPen);
        Assert.Same(themeValue(theme, ChartTokens.LegendForeground), Lines(legend)[0].Foreground);

        // A host's own theme under the same key is what the legend wears instead: no property of the host's
        // page has to change, and the legend's own items keep the theme they were given.
        var mine = new ControlTheme(typeof(Legend))
        {
            BasedOn = (ControlTheme)theme[typeof(Legend)]!,
            Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.HotPink) },
        };

        var (dressed, _, _) = Show(new ResourceDictionary { [typeof(Legend)] = mine }, Named("Depth"));

        Assert.Same(Brushes.HotPink, dressed.Background);
        Assert.Equal(["Depth"], Lines(dressed).Select(item => item.Text!).ToArray());
    }

    /// <summary>The lines of a legend, which are its items.</summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The lines, in the order they are listed.</returns>
    private static List<LegendItem> Lines(Legend legend) => [.. legend.Items.OfType<LegendItem>()];

    [Fact]
    public void A_legend_under_the_plot_runs_its_lines_across_it_and_leaves_the_chart_room()
    {
        // A legend is read as a row when it is above or below the plot, and the two are one picture: a legend
        // that touches the chart is a legend a reader has to pick apart from it, so the room between them is part
        // of the drawing rather than something every host has to remember to leave.
        var (legend, chart) = Placed(LegendPlacement.Bottom, Dock.Bottom);
        var lines = Lines(legend);

        Assert.True(
            lines[1].Bounds.Left > lines[0].Bounds.Left,
            $"the lines are laid out down each other at {lines[0].Bounds} and {lines[1].Bounds}");
        Assert.Equal(lines[0].Bounds.Top, lines[1].Bounds.Top, 1);

        var gap = legend.Bounds.Top - chart.Bounds.Bottom;

        Assert.True(gap >= 6, $"the legend sits {gap:0} px under the chart, which is touching it");
    }

    [Fact]
    public void A_legend_beside_the_plot_runs_its_lines_down_it_and_leaves_the_chart_room()
    {
        var (legend, chart) = Placed(LegendPlacement.Right, Dock.Right);
        var lines = Lines(legend);

        Assert.True(
            lines[1].Bounds.Top > lines[0].Bounds.Top,
            $"the lines are laid out across each other at {lines[0].Bounds} and {lines[1].Bounds}");
        Assert.Equal(lines[0].Bounds.Left, lines[1].Bounds.Left, 1);

        var gap = legend.Bounds.Left - chart.Bounds.Right;

        Assert.True(gap >= 6, $"the legend sits {gap:0} px beside the chart, which is touching it");
    }

    /// <summary>A page with a legend on one side of a chart, laid out and measured.</summary>
    /// <param name="placement">The side of the plot the legend is on.</param>
    /// <param name="dock">Where the page puts it, which is what a host would write.</param>
    /// <returns>The legend and the chart.</returns>
    private static (Legend Legend, CartesianChart Chart) Placed(LegendPlacement placement, Dock dock)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
        };

        chart.Series.Add(Named("Depth"));
        chart.Series.Add(Named("Speed"));

        var legend = new Legend { Chart = chart, Placement = placement };
        var page = new DockPanel();

        DockPanel.SetDock(legend, dock);
        page.Children.Add(legend);
        page.Children.Add(chart);

        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(ChartTheme.Create());

        _ = new Window { Resources = resources, Content = page };

        page.Measure(new Size(600, 300));
        page.Arrange(new Rect(0, 0, 600, 300));

        return (legend, chart);
    }

    /// <summary>A value out of the theme, in the light variant.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token's name.</param>
    /// <returns>The value.</returns>
    private static object themeValue(ResourceDictionary theme, string token)
    {
        Assert.True(theme.TryGetResource(token, ThemeVariant.Light, out var value), $"{token} is not defined");
        return value!;
    }

    /// <summary>A series with a name, so a legend has something to write.</summary>
    /// <param name="name">What it is called.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Named(string name) =>
        new()
        {
            DisplayName = name,
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => value,
            YSelector = value => value,
        };

    /// <summary>A page with a legend beside a chart, dressed the way a host dresses one.</summary>
    /// <param name="series">The series to put on the chart.</param>
    /// <returns>The legend, the chart, and the theme.</returns>
    private static (Legend Legend, CartesianChart Chart, ResourceDictionary Theme) Show(
        params LineSeries<double>[] series) => Show(null, series);

    /// <summary>A page with a legend beside a chart, with resources of its own on the legend.</summary>
    /// <param name="overrides">Resources for the legend itself, as a host with a theme of its own would set.</param>
    /// <param name="series">The series to put on the chart.</param>
    /// <returns>The legend, the chart, and the theme.</returns>
    private static (Legend Legend, CartesianChart Chart, ResourceDictionary Theme) Show(
        ResourceDictionary? overrides,
        params LineSeries<double>[] series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        var legend = new Legend { Chart = chart };

        if (overrides is not null)
        {
            legend.Resources = overrides;
        }
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Left);
        page.Children.Add(legend);
        page.Children.Add(chart);

        var theme = ChartTheme.Create();
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(theme);

        // The window is what dresses the page: a control theme reaches a control the way it reaches one in an
        // application, which is the arrangement these tests are about.
        _ = new Window { Resources = resources, Content = page };

        page.Measure(new Size(600, 200));
        page.Arrange(new Rect(0, 0, 600, 200));
        return (legend, chart, theme);
    }
}
