using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the colour tokens against the table the design brief froze, and checks that they reach a chart.
/// </summary>
/// <remarks>
/// The token names and their types are a contract with every host: a host overrides a token by name, so a token
/// that disappears - or that quietly changes from a brush to a pen - breaks a host rather than a chart. The
/// two lists below are that contract written out. A new token has to be put in one of them, because the last
/// test here insists they cover every name the library publishes.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ChartThemeTests
{
    /// <summary>The tokens that are strokes, and therefore <see cref="Pen"/>s.</summary>
    private static readonly string[] StrokeTokens =
    [
        ChartTokens.SurfaceBorder,
        ChartTokens.SurfaceFocusRing,
        ChartTokens.AxisLine,
        ChartTokens.AxisTick,
        ChartTokens.AxisGridLineMajor,
        ChartTokens.AxisGridLineMinor,
        ChartTokens.AxisGridLineOrigin,
        ChartTokens.MarkerStroke,
        ChartTokens.MarkerHoverStroke,
        ChartTokens.MarkerSelectedStroke,
        ChartTokens.CrosshairLine,
        ChartTokens.LegendBorder,
        ChartTokens.DataTipBorder,
        ChartTokens.SelectionRubberBandStroke,
    ];

    /// <summary>The tokens that are fills, and therefore <see cref="IBrush"/>es.</summary>
    private static readonly string[] FillTokens =
    [
        ChartTokens.SurfaceBackground,
        ChartTokens.AxisBandFill,
        ChartTokens.AxisBandFillAlt,
        ChartTokens.AxisLabelForeground,
        ChartTokens.AxisTitleForeground,
        ChartTokens.MarkerFill,
        ChartTokens.MarkerHoverFill,
        ChartTokens.MarkerSelectedFill,
        ChartTokens.CrosshairLabelBackground,
        ChartTokens.CrosshairLabelForeground,
        ChartTokens.LegendBackground,
        ChartTokens.LegendForeground,
        ChartTokens.LegendItemDisabled,
        ChartTokens.DataTipBackground,
        ChartTokens.DataTipForeground,
        ChartTokens.DataTipShadow,
        ChartTokens.FinancialUp,
        ChartTokens.FinancialDown,
        ChartTokens.SelectionRubberBandFill,
    ];

    private static readonly ThemeVariant[] Variants = [ThemeVariant.Light, ThemeVariant.Dark];

    [Fact]
    public void Every_named_token_is_defined_in_both_variants()
    {
        var resources = ChartTheme.Create();

        foreach (var token in NamedTokens())
        {
            foreach (var variant in Variants)
            {
                Assert.True(
                    resources.TryGetResource(token, variant, out var value),
                    $"{token} has no value in the {variant} variant");
                Assert.NotNull(value);
            }
        }

        foreach (var slot in Slots())
        {
            foreach (var token in new[] { ChartTokens.SeriesStroke(slot), ChartTokens.SeriesFill(slot) })
            {
                foreach (var variant in Variants)
                {
                    Assert.True(
                        resources.TryGetResource(token, variant, out var value),
                        $"{token} has no value in the {variant} variant");
                    Assert.NotNull(value);
                }
            }
        }
    }

    [Fact]
    public void A_token_is_a_pen_when_it_strokes_and_a_brush_when_it_fills()
    {
        var resources = ChartTheme.Create();

        foreach (var variant in Variants)
        {
            foreach (var token in StrokeTokens)
            {
                Assert.IsType<Pen>(Value(resources, token, variant));
            }

            foreach (var token in FillTokens)
            {
                Assert.IsAssignableFrom<IBrush>(Value(resources, token, variant));
            }
        }

        // The two lists are the whole table: a token that is in neither is a token nobody decided the type of.
        Assert.Equal(
            NamedTokens().Order(),
            StrokeTokens.Concat(FillTokens).Order());
    }

    [Fact]
    public void The_palette_holds_ten_strokes_and_ten_fills_that_can_be_told_apart()
    {
        var resources = ChartTheme.Create();

        foreach (var variant in Variants)
        {
            var colours = new List<Color>();

            foreach (var slot in Slots())
            {
                var stroke = Assert.IsType<Pen>(Value(resources, ChartTokens.SeriesStroke(slot), variant));
                var fill = Assert.IsAssignableFrom<ISolidColorBrush>(Value(resources, ChartTokens.SeriesFill(slot), variant));

                // The fill is the stroke at 47%, which is what makes a filled series look like the line over it.
                var brush = Assert.IsAssignableFrom<ISolidColorBrush>(stroke.Brush);

                Assert.Equal(brush.Color.R, fill.Color.R);
                Assert.Equal(brush.Color.G, fill.Color.G);
                Assert.Equal(brush.Color.B, fill.Color.B);
                Assert.InRange(fill.Color.A, 0x70, 0x80);
                colours.Add(brush.Color);
            }

            Assert.Equal(colours.Count, colours.Distinct().Count());
        }
    }

    [Fact]
    public void The_control_theme_dresses_a_chart_in_the_themes_inks()
    {
        var theme = ChartTheme.Create();
        var chart = Dressed(theme);

        Assert.Same(Value(theme, ChartTokens.SurfaceBackground, ThemeVariant.Light), chart.Background);
        Assert.Same(Value(theme, ChartTokens.SurfaceBorder, ThemeVariant.Light), chart.BorderPen);
        Assert.Same(Value(theme, ChartTokens.AxisLabelForeground, ThemeVariant.Light), chart.LabelBrush);

        // And the chart hands them down to the layers that paint the axes, which is the half a control theme
        // cannot reach by itself.
        Assert.Same(chart.GridLinePen, chart.VerticalLayer.GridLinePen);
        Assert.Same(chart.GridLinePen, chart.HorizontalLayer.GridLinePen);
        Assert.Same(chart.MinorGridLinePen, chart.VerticalLayer.MinorGridLinePen);
        Assert.Same(chart.AxisLinePen, chart.HorizontalLayer.AxisLinePen);
        Assert.Same(chart.TickPen, chart.VerticalLayer.TickPen);
        Assert.Same(chart.LabelBrush, chart.HorizontalLayer.LabelBrush);
    }

    [Fact]
    public void A_host_reskins_a_chart_by_overriding_a_token()
    {
        // The whole point of tokens: a chart can be dressed differently without a single new API, by putting a
        // value under a key it already reads.
        var pink = new Pen(Brushes.HotPink, 3);
        var overrides = new ResourceDictionary { [ChartTokens.AxisGridLineMajor] = pink };

        var chart = Dressed(ChartTheme.Create(), overrides);

        Assert.Same(pink, chart.GridLinePen);
        Assert.Same(pink, chart.VerticalLayer.GridLinePen);
    }

    [Fact]
    public void The_theme_loads_the_way_a_host_includes_it()
    {
        // What a ResourceInclude in a host's XAML compiles to: the dictionary has to be reachable by its
        // avares:// name, not only through the property that happens to hold it.
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceInclude((Uri?)null) { Source = ChartTheme.Source });

        Assert.NotNull(Value(resources, ChartTokens.SurfaceBackground, ThemeVariant.Light));
        Assert.NotNull(Value(resources, ChartTokens.SeriesStroke(ChartTokens.PaletteSize), ThemeVariant.Dark));
    }

    private static IEnumerable<int> Slots() => Enumerable.Range(1, ChartTokens.PaletteSize);

    /// <summary>Every token name the library publishes as a constant.</summary>
    private static IEnumerable<string> NamedTokens() =>
        typeof(ChartTokens)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => (string)field.GetValue(null)!);

    private static object Value(ResourceDictionary resources, string token, ThemeVariant variant)
    {
        Assert.True(resources.TryGetResource(token, variant, out var value), $"{token} is not defined");
        return value!;
    }

    /// <summary>A chart wearing the theme, the way a host would dress it.</summary>
    /// <param name="theme">The theme, as a host would merge it into its application resources.</param>
    /// <param name="overrides">Tokens to put in the chart's own resources, as a host would for one chart.</param>
    /// <returns>The chart, laid out once and dressed.</returns>
    private static CartesianChart Dressed(ResourceDictionary theme, ResourceDictionary? overrides = null)
    {
        var chart = new CartesianChart();

        if (overrides is not null)
        {
            chart.Resources = overrides;
        }

        // The theme is in scope above the chart, which is where an application puts it, and the chart takes its
        // control theme from there when it joins the tree.
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(theme);

        new Window { Resources = resources, Content = chart };
        chart.Measure(new Avalonia.Size(200, 100));
        chart.Arrange(new Avalonia.Rect(0, 0, 200, 100));

        return chart;
    }
}
