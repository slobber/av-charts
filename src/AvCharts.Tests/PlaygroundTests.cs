using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvCharts.Demo;
using AvCharts.Themes;
using iPaas.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the knobs on the gallery's playground: that a group of them keeps exactly one on, whatever is clicked,
/// and that clicking one does not take the application down with it.
/// </summary>
/// <remarks>
/// The second half of that is the reason this class exists. Turning the other buttons off is a change to their
/// own state, and their handlers turned them straight back on - so clicking one of these threw a
/// <c>StackOverflowException</c> and there was no catching it. A test that clicks one is therefore a test that
/// the application survives being used.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class PlaygroundTests
{
    [Fact]
    public void One_button_of_a_group_is_on_whatever_is_clicked()
    {
        var page = Show("Lines");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var groups = page.GetVisualDescendants().OfType<ToggleGroup>().ToArray();

        Assert.NotEmpty(groups);

        foreach (var group in groups)
        {
            var buttons = group.GetVisualDescendants().OfType<Toggle>().ToArray();

            Assert.True(buttons.Length > 1, "a group should offer more than one choice");

            for (var clicked = 0; clicked < buttons.Length; clicked++)
            {
                buttons[clicked].IsChecked = true;

                Assert.True(buttons[clicked].IsChecked == true, "the button that was picked should be on");
                Assert.Single(buttons, button => button.IsChecked == true);

                // And clicking the one that is already on leaves it on, because a group of buttons read as one
                // control has no "none of them" state.
                buttons[clicked].IsChecked = false;

                Assert.True(buttons[clicked].IsChecked == true, "the last button cannot be turned off");
                Assert.Single(buttons, button => button.IsChecked == true);
            }
        }
    }

    [Fact]
    public void A_knob_that_changes_the_drawing_asks_for_a_repaint()
    {
        // The knobs were changing the chart's settings and the chart was not painting again, so every one of
        // them looked broken except the curve - which invalidates its own geometry, and was the only one that
        // appeared to work. What is asserted here is the outcome: the picture changes when a knob is turned.
        var page = Show("Lines");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var chart = page.GetVisualDescendants().OfType<AvCharts.Controls.CartesianChart>()
            .Single(one => one.Series.Count == 2);
        var layer = chart.VerticalLayer;
        var before = Picture(layer);

        layer.ShowGridLines = false;
        chart.VerticalRenderer.ShowLabels = false;
        chart.InvalidateArrange();
        layer.InvalidateVisual();

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var after = Picture(layer);

        Assert.True(Ink.Count(before) > Ink.Count(after), "turning the vertical grid off should remove ink from the picture");
    }
    [Fact]
    public void A_switch_and_a_picker_are_the_house_s_own_controls()
    {
        // The knobs are iPaas.Controls rather than bare Avalonia ones, so that the demo looks like the rest of
        // the house's demos - and so that a reader sees the controls they would use.
        var page = Show("Lines");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        Assert.NotEmpty(page.GetVisualDescendants().OfType<Switch>());
        Assert.NotEmpty(page.GetVisualDescendants().OfType<Select>());
    }

    /// <summary>Draws a chart, so that a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(Avalonia.Layout.Layoutable chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Catalog.PageWidth, Catalog.PageHeight));
        bitmap.Render(chart);
        return bitmap;
    }
    /// <summary>Shows a page of the gallery the way the demo window shows it.</summary>
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
}
