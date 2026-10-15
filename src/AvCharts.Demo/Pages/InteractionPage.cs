using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The page of tips, a legend and a crosshair.
internal static class InteractionPage
{
    /// <summary>Tips, a legend, and a crosshair: what a reader gets when they point at the chart.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var page = new DockPanel();
        var chart = Chart(0, 6, 0, 10);

        chart.Series.Add(Line("Depth", [2.0, 5.0, 3.0, 7.0, 6.0, 9.0]));
        chart.Series.Add(Line("Speed", [1.0, 2.0, 1.5, 3.0, 2.5, 4.0]));
        chart.ShowCrosshair = true;
        chart.SelectionMode = ChartSelectionMode.Single;

        // Every reading is written out, because a page is drawn once and there is no pointer to hover with: in the
        // window a reader points at a chart and gets one tip at a time.
        chart.ShowAllDataTips = true;

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(legend, Dock.Right);

        page.Children.Add(legend);
        page.Children.Add(chart);
        return Page(
            "Interaction",
            "What a reader gets when they point at a chart.",
            Example("A tip per reading, a legend, and a crosshair", page),
            OwnTip(),
            Try("Try it: the tips, the crosshair, the legend and the selection", InteractionPlayground()));
    }

    /// <summary>A tip a host wrote: what it says, and what shape it is.</summary>
    /// <returns>The block.</returns>
    /// <remarks>
    /// <para>
    /// Both halves of a tip are a host's: what it writes, which is a function of the hit, and how it looks, which
    /// is a control theme of its own. The one here writes two lines - what the series is called, and the reading -
    /// and wears a two pixel outline with a shadow behind it.
    /// </para>
    /// <para>
    /// The theme is put in the resources of this chart rather than of the application, which is the same rule as
    /// a token: a host can dress one chart's tips without touching anybody else's.
    /// </para>
    /// </remarks>
    private static DemoBlock OwnTip()
    {
        var chart = Chart(0, 6, 0, 10);

        chart.Series.Add(Line("Depth", [2.0, 5.0, 3.0, 7.0, 6.0, 9.0]));
        chart.Series.Add(Line("Speed", [1.0, 2.0, 1.5, 3.0, 2.5, 4.0]));

        // A host is handed the hit - the record, the series it belongs to, where on the chart it is - and writes
        // the line itself. Two lines here, so the reader gets the name of the thing before the number of it.
        chart.DataTipFunction = hit => $"{hit.ChartItem.Series.DisplayName}\n{hit.Item}";

        chart.Resources = new ResourceDictionary { [typeof(DataTip)] = OwnTipTheme() };

        // Every reading, because a page is drawn once and there is no pointer to hover with.
        chart.ShowAllDataTips = true;

        return Example("A tip of your own: the series, the reading, a border and a shadow", chart);
    }

    /// <summary>The tip's look, written by hand: white, a two pixel outline, a shadow, and two lines of text.</summary>
    /// <returns>The control theme the chart looks up by type.</returns>
    /// <remarks>
    /// A host writing one of these in markup writes a <c>ControlTheme</c> under the key <c>{x:Type DataTip}</c>
    /// with a template in it; this is the same thing built in code, which is what the gallery can do.
    /// </remarks>
    private static ControlTheme OwnTipTheme()
    {
        var outline = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4));

        return new ControlTheme(typeof(DataTip))
        {
            Setters =
            {
                new Setter(DataTip.BackgroundProperty, Brushes.White),
                new Setter(DataTip.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x24))),
                new Setter(DataTip.BorderPenProperty, new Pen(outline, 2)),
                new Setter(DataTip.PaddingProperty, new Thickness(9, 6)),
                new Setter(
                    DataTip.TemplateProperty,
                    new FuncControlTemplate<DataTip>((tip, _) => Surface(tip, outline))),
            },
        };
    }

    /// <summary>The box a tip of this chart is: the outline, the shadow, and the two lines inside them.</summary>
    /// <param name="tip">The tip the template is for.</param>
    /// <param name="outline">The colour its outline is drawn in.</param>
    /// <returns>The template's root.</returns>
    private static Border Surface(DataTip tip, IBrush outline)
    {
        var text = new TextBlock { FontSize = 11, LineHeight = 15 };

        // Written the way a template writes it: the tip says what the line is, and the template listens.
        text.Bind(TextBlock.TextProperty, tip.GetObservable(DataTip.TextProperty));
        text.Bind(TextBlock.ForegroundProperty, tip.GetObservable(DataTip.ForegroundProperty));

        var surface = new Border
        {
            Background = Brushes.White,
            BorderBrush = outline,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 6),
            BoxShadow = BoxShadows.Parse("0 3 10 0 #40000000"),
            Child = text,
        };

        return surface;
    }

    /// <summary>
    /// The same chart with the four ways of answering a pointer on knobs.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// Every one of these is a setting a host turns on for its own chart, so they are worth a switch each: what a
    /// tip is written for, whether a crosshair follows the pointer, whether a click selects a reading or a run of
    /// them, and whether the legend is there to say which line is which.
    /// </remarks>
    private static Control InteractionPlayground()
    {
        var chart = Chart(0, 6, 0, 10);

        chart.Series.Add(Line("Depth", [2.0, 5.0, 3.0, 7.0, 6.0, 9.0]));
        chart.Series.Add(Line("Speed", [1.0, 2.0, 1.5, 3.0, 2.5, 4.0]));
        chart.ShowCrosshair = true;
        chart.SelectionMode = ChartSelectionMode.Single;

        void Tips(string picked)
        {
            chart.ShowAllDataTips = picked == "every reading";
            chart.DataTipMode = picked switch
            {
                "every reading" => DataTipMode.Item,
                "the pointer" => DataTipMode.Mouse,
                _ => DataTipMode.None,
            };
        }

        Tips("every reading");

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Right);
        page.Children.Add(legend);
        page.Children.Add(chart);

        var selections = new[] { ChartSelectionMode.Single, ChartSelectionMode.Multiple, ChartSelectionMode.None };

        return Playground(
            page,
            Choice("Tips", ["every reading", "the pointer", "none"], "every reading", Tips),
            OnOff("Crosshair", true, on => chart.ShowCrosshair = on),
            Choices("Selection", ["one", "many", "off"], 0, picked => chart.SelectionMode = selections[picked]),
            OnOff("Legend", true, on => legend.IsVisible = on));
    }
}
