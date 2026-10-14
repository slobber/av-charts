using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;

namespace AvCharts.Demo;

// The theme page: one chart, dressed by the variant under it and by tokens a host put above it.
public static partial class Pages
{
    /// <summary>One chart in either variant, and in colours a host brought instead of the theme's own.</summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// <para>
    /// A chart is dressed from the resources above it: the tokens the theme supplies, and whatever a host has put
    /// in the resources of one chart instead. So changing how a chart looks is changing those, and nothing about
    /// the chart itself - the same series, the same axes, the same legend, in either variant or in somebody's own
    /// colours.
    /// </para>
    /// <para>
    /// The variant is put above the chart rather than on the application, because the demo is one window: a host
    /// with a dark chart on a light page does it this way - `ThemeVariantScope` is the control for exactly that -
    /// and a host that wants the whole window to change sets it where the whole window can see it.
    /// </para>
    /// </remarks>
    internal static DemoPage Theming() => Page(
        "Theme",
        "One chart, in either variant and in a host's own colours.",
        Try("Try it: the theme under the chart, and the tokens above it", ThemingPlayground()));

    /// <summary>The chart with a knob for the variant and a knob for whose tokens it wears.</summary>
    /// <returns>The playground.</returns>
    private static Control ThemingPlayground()
    {
        var stage = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Light };
        var report = new TextBlock { FontSize = 11, Opacity = 0.6 };
        var variants = new[] { ThemeVariant.Light, ThemeVariant.Dark };

        var showing = 0;
        var own = false;

        void Show()
        {
            stage.Child = Dressed(own);
            report.Text =
                $"{(showing == 0 ? "light" : "dark")} · "
                + (own ? "the host's colours" : "the theme's colours")
                + ".";
        }

        Show();

        return Playground(
            stage,
            Choice("Variant", ["light", "dark"], "light", picked => { showing = picked == "dark" ? 1 : 0; Show(); }),
            Choice("Tokens", ["the theme's", "the host's"], "the theme's", picked => { own = picked == "the host's"; Show(); }),
            Labelled("What is showing", report));
    }

    /// <summary>A chart of two series, dressed for one variant and in one set of tokens.</summary>
    /// <param name="ownTokens">Whether the chart wears a host's tokens instead of the theme's.</param>
    /// <returns>The chart with its legend, which is what a page puts on screen.</returns>
    /// <remarks>
    /// A new chart each time rather than one chart re-dressed: a chart takes its colours when it joins a tree, so
    /// the honest way to show another set of them is to hand it another set from the start. Changing the variant
    /// under a chart that is already drawn does re-dress it, which is the knob beside this one.
    /// </remarks>
    private static Control Dressed(bool ownTokens)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        chart.Series.Add(Line("Depth", [2, 5, 3, 7, 6, 9]));
        chart.Series.Add(Line("Speed", [1, 2, 1.5, 3, 2.5, 4]));

        if (ownTokens)
        {
            var green = new SolidColorBrush(Color.FromRgb(0x6F, 0xD1, 0xC0));

            chart.Resources = new ResourceDictionary
            {
                [ChartTokens.SurfaceBackground] = new SolidColorBrush(Color.FromRgb(0x10, 0x18, 0x20)),
                [ChartTokens.AxisGridLineMajor] = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x46)), 1),
                [ChartTokens.AxisLabelForeground] = new SolidColorBrush(Color.FromRgb(0xB8, 0xC6, 0xD0)),
                [ChartTokens.SeriesStroke(1)] = new Pen(green, 2),
                [ChartTokens.SeriesFill(1)] = new SolidColorBrush(green.Color, 0.47),
                [ChartTokens.LegendBorder] = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x46)), 1),
            };
        }

        var legend = new Legend { Chart = chart };
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Left);
        page.Children.Add(legend);
        page.Children.Add(chart);
        return page;
    }
}
