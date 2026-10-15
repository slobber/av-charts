using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvCharts.Demo;
using AvCharts.Demo.Examples.Lines;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the gallery's markup examples: that the three tabs are the picture, the code and the markup, and that
/// the picture is the same chart as the code draws.
/// </summary>
/// <remarks>
/// A gallery of examples is a kind of documentation that stops being true quietly - a snippet that no longer
/// builds, a markup file that no longer draws what its code half draws. What is checked here is the two halves
/// of that: the tabs are there, and the two halves of an example leave the same ink.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ExampleTests
{
    [Fact]
    public void An_example_offers_its_picture_its_code_and_its_markup()
    {
        var page = Show("Lines");

        Layout(page);

        var tabs = page.GetVisualDescendants().OfType<TabControl>().Single();
        var headers = tabs.Items.OfType<TabItem>().Select(tab => tab.Header).ToArray();

        Assert.Equal(new object[] { "Result", "Code", "axaml" }, headers);

        // And the markup tab shows the file that the picture was drawn from, rather than a copy of it: the text
        // that is on screen is what the build compiled.
        var shown = Text(tabs, 2);
        var file = Read("Lines/LineWithMarkers", "axaml");

        Assert.Equal(file, shown);
        Assert.Contains("<avc:LineSeries", shown, StringComparison.Ordinal);

        // And the code tab the file beside it.
        var code = Text(tabs, 1);

        Assert.Equal(Read("Lines/LineWithMarkers", "cs"), code);
    }

    [Fact]
    public void An_example_drawn_from_markup_is_the_example_drawn_in_code()
    {
        // The promise the two tabs make between them: a chart written as markup and the same chart written in
        // code are the same chart, not two that look alike. Drawn at the same size and compared pixel by pixel -
        // which is also what stops the code half of an example from drifting away from its markup half.
        var markup = Drawn(
            (Control)AvaloniaXamlLoader.Load(new Uri("avares://AvChartsDemo/Examples/Lines/LineWithMarkers.axaml")));
        var code = Drawn(new LineWithMarkers().Build());

        Assert.Equal(0, Ink.Differences(markup, code));
        Assert.True(Ink.Count(code) > 0, "the example drew something");
    }

    /// <summary>The text one tab of a block shows.</summary>
    /// <param name="tabs">The block's tabs.</param>
    /// <param name="at">Which tab.</param>
    /// <returns>The text.</returns>
    private static string Text(TabControl tabs, int at) =>
        ((SelectableTextBlock)((ScrollViewer)tabs.Items.OfType<TabItem>().ElementAt(at).Content!).Content!).Text!;
    /// <summary>Draws a control the way a page draws one.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Drawn(Control control)
    {
        _ = new Window { Resources = ChartTheme.Create(), Content = control };

        control.Measure(new Size(560, 300));
        control.Arrange(new Rect(0, 0, 560, 300));

        var bitmap = new RenderTargetBitmap(new PixelSize(560, 300));

        bitmap.Render(control);
        return bitmap;
    }

    /// <summary>One of the gallery's example files, as the tabs read it.</summary>
    /// <param name="name">The example's name under <c>Examples/</c>.</param>
    /// <param name="extension">The extension, without a dot.</param>
    /// <returns>The text.</returns>
    private static string Read(string name, string extension)
    {
        var address = typeof(Gallery).Assembly.GetManifestResourceNames()
            .Single(one => one.EndsWith($"{name.Replace('/', '.')}.{extension}", StringComparison.Ordinal));

        using var stream = typeof(Gallery).Assembly.GetManifestResourceStream(address)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Shows a page of the gallery the way the demo window does.</summary>
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
            Styles = { new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        return page;
    }

    /// <summary>Lays a page out, and every example in it with it.</summary>
    /// <param name="page">The page.</param>
    private static void Layout(Control page)
    {
        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));
    }
}
