using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>A chart wearing the default theme, dressed the way a host would dress one.</summary>
/// <remarks>
/// The theme goes into the resources of a window the chart is put in - the arrangement an application makes -
/// and the chart takes its control theme from there when it joins the tree. The window is kept because changing
/// its <c>RequestedThemeVariant</c> is how a test changes the variant under a chart.
/// </remarks>
internal sealed class ThemedChart
{
    /// <summary>Dresses a chart and lays it out once.</summary>
    /// <param name="overrides">Tokens to put in the chart's own resources, as a host would for one chart.</param>
    /// <param name="chart">How to make the chart, when a test needs one it can drive itself.</param>
    public ThemedChart(ResourceDictionary? overrides = null, Func<CartesianChart>? chart = null)
    {
        Theme = ChartTheme.Create();
        Chart = chart?.Invoke() ?? new CartesianChart();

        if (overrides is not null)
        {
            Chart.Resources = overrides;
        }

        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(Theme);

        Window = new Window { Resources = resources, Content = Chart };
        Layout();
    }

    /// <summary>The theme, as a host would merge it into its application resources.</summary>
    public ResourceDictionary Theme { get; }

    /// <summary>The chart, dressed and laid out.</summary>
    public CartesianChart Chart { get; }

    /// <summary>The window the chart is in; its variant is the one the chart shows.</summary>
    public Window Window { get; }

    /// <summary>Lays the chart out again, at the size it was given.</summary>
    public void Layout()
    {
        Chart.Measure(new Size(400, 200));
        Chart.Arrange(new Rect(0, 0, 400, 200));
    }

    /// <summary>The value of one token in one variant of the theme.</summary>
    /// <param name="token">The token's name.</param>
    /// <param name="variant">The variant to read it in.</param>
    /// <returns>The value.</returns>
    public object Value(string token, ThemeVariant variant)
    {
        Assert.True(Theme.TryGetResource(token, variant, out var value), $"{token} is not defined");
        return value!;
    }
}
