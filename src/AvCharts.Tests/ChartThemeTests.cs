using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvCharts.Charts;
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
        var themed = new ThemedChart();
        var chart = themed.Chart;

        Assert.Same(themed.Value(ChartTokens.SurfaceBackground, ThemeVariant.Light), chart.Background);
        Assert.Same(themed.Value(ChartTokens.SurfaceBorder, ThemeVariant.Light), chart.BorderPen);
        Assert.Same(themed.Value(ChartTokens.AxisLabelForeground, ThemeVariant.Light), chart.LabelBrush);

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

        var chart = new ThemedChart(overrides).Chart;

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

    [Fact]
    public void A_pen_reaches_the_border_that_draws_it()
    {
        // The outline of a tip and of a legend is a token, and a token is a pen: a brush and how wide. The
        // template reads those two out of it, and this is both halves of that - the border carries the pen's
        // brush and its width, and nothing was logged while the theme was applied. It used to be neither: the
        // binding walked into the pen, so every tip of every chart logged an error and the thickness it handed
        // a border was a number where a thickness was wanted, which is an outline that is never drawn.
        var logged = new List<string>();
        var sink = Logger.Sink;

        Logger.Sink = new LogProbe(logged);

        try
        {
            var chart = new CartesianChart();

            chart.Series.Add(new LineSeries<double>
            {
                ItemsSource = [1.0, 2.0, 3.0],
                YSelector = value => value,
                Marker = Markers.Circle,
            });

            chart.ShowAllDataTips = true;

            var legend = new Legend { Chart = chart };
            var page = new DockPanel();

            DockPanel.SetDock(legend, Dock.Left);
            page.Children.Add(legend);
            page.Children.Add(chart);

            // Dressed the way a host dresses them: the theme in the resources of something above both.
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(ChartTheme.Create());
            _ = new Window { Resources = resources, Content = page };

            page.Measure(new Size(600, 300));
            page.Arrange(new Rect(0, 0, 600, 300));

            // Drawn as well as laid out, since a binding that only fails at the last moment would otherwise be
            // missed - and painting is what a tip is for.
            using (var picture = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(600, 300)))
            {
                picture.Render(page);
            }

            var tip = page.GetVisualDescendants().OfType<DataTip>().First();
            var line = page.GetVisualDescendants().OfType<Legend>().First();

            Assert.NotNull(tip.BorderPen);
            Assert.Equal(new Thickness(tip.BorderPen!.Thickness), Outlined(tip).BorderThickness);
            Assert.Same(tip.BorderPen.Brush, Outlined(tip).BorderBrush);

            Assert.NotNull(line.BorderPen);
            Assert.Equal(new Thickness(line.BorderPen!.Thickness), Outlined(line).BorderThickness);
            Assert.Same(line.BorderPen.Brush, Outlined(line).BorderBrush);
        }
        finally
        {
            Logger.Sink = sink;
        }

        Assert.Empty(logged);
    }

    /// <summary>The box a control's theme draws its outline on, which is the part it is named by.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The border.</returns>
    /// <remarks>
    /// <c>PART_Surface</c> is the contract between a control and its theme: the control puts the pen it was given
    /// on that box as a brush and a thickness, and a host writing a theme of its own names the same part.
    /// </remarks>
    private static Border Outlined(Control control) =>
        control.GetVisualDescendants().OfType<Border>().First(one => one.Name == "PART_Surface");

    /// <summary>Keeps what the framework logged, so that "it logged nothing" can be asserted.</summary>
    /// <param name="messages">Where the messages go.</param>
    private sealed class LogProbe(List<string> messages) : ILogSink
    {
        /// <inheritdoc />
        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

        /// <inheritdoc />
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            messages.Add($"{level} {area}: {messageTemplate}");

        /// <inheritdoc />
        public void Log(
            LogEventLevel level,
            string area,
            object? source,
            string messageTemplate,
            params object?[] propertyValues) =>
            messages.Add($"{level} {area}: {messageTemplate} ({string.Join(", ", propertyValues)})");
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
}
