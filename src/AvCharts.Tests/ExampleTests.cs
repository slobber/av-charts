using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvCharts.Demo;
using AvCharts.Demo.Examples;
using AvCharts.Themes;
using iPaas.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the gallery's markup examples: that the three tabs are the picture, the code and the markup, and that
/// the picture is the same chart as the code draws.
/// </summary>
/// <remarks>
/// <para>
/// A gallery of examples is a kind of documentation that stops being true quietly - a snippet that no longer
/// builds, a markup file that no longer draws what its code half draws. What is checked here is the two halves of
/// that: the tabs are there, and the two halves of an example leave the same ink.
/// </para>
/// <para>
/// The examples are found by what they are - see <see cref="IExample"/> - rather than listed here, so that a pair
/// of files dropped into the gallery is checked without anybody remembering to add it to this file as well.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ExampleTests
{
    [Fact]
    public void An_example_offers_its_picture_its_code_and_its_markup()
    {
        foreach (var (name, _) in Examples())
        {
            var page = Show(Page(name));

            Layout(page);

            var tabs = Tabs(page, name);
            var headers = tabs.Items.OfType<TabItem>().Select(tab => tab.Header).ToArray();

            Assert.Equal(new object[] { "Result", "Code", "axaml" }, headers);

            // The markup tab shows the file the picture was drawn from, rather than a copy of it: the text that
            // is on screen is what the build compiled.
            var markup = File(name, "axaml");

            Assert.Equal(markup, Text(tabs, 2));
            Assert.Contains("<avc:", markup, StringComparison.Ordinal);

            // And the code tab the file beside it.
            Assert.Equal(File(name, "cs"), Text(tabs, 1));
        }
    }

    [Fact]
    public void An_example_drawn_from_markup_is_the_example_drawn_in_code()
    {
        // The promise the two tabs make between them: a chart written as markup and the same chart written in
        // code are the same chart, not two that look alike. Drawn at the same size and compared pixel by pixel -
        // which is also what stops the code half of an example from drifting away from its markup half.
        foreach (var (name, code) in Examples())
        {
            var markup = Drawn((Control)AvaloniaXamlLoader.Load(new Uri($"avares://AvChartsDemo/Examples/{name}.axaml")));
            var drawn = Drawn(code.Build());

            Assert.Equal(0, Ink.Differences(markup, drawn));
            Assert.True(Ink.Count(drawn) > 0, $"{name} drew something");
        }
    }

    /// <summary>Every example the gallery has: its name, and the code half of it.</summary>
    /// <returns>The examples, in a settled order.</returns>
    /// <remarks>
    /// Read out of the demo's own assembly as the types that are examples, which is what makes a new pair of files
    /// an example and nothing else: a list kept here would be a place for one to be left out of.
    /// </remarks>
    private static IEnumerable<(string Name, IExample Code)> Examples() =>
        typeof(IExample).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(IExample).IsAssignableFrom(type))
            .Select(type => (
                Name: type.FullName!["AvCharts.Demo.Examples.".Length..].Replace('.', '/'),
                Code: (IExample)Activator.CreateInstance(type)!))
            .OrderBy(example => example.Name, StringComparer.Ordinal);

    /// <summary>The page an example is shown on, which is the folder its files are in.</summary>
    /// <param name="name">The example's name under <c>Examples/</c>.</param>
    /// <returns>The page's name.</returns>
    private static string Page(string name) => name[..name.IndexOf('/', StringComparison.Ordinal)];

    /// <summary>The tabs of the block that shows one example.</summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The example's name under <c>Examples/</c>.</param>
    /// <returns>The block's tabs.</returns>
    /// <remarks>The markup tab is what tells one block from another: it carries the file's own text.</remarks>
    private static Tabs Tabs(Control page, string name)
    {
        var markup = File(name, "axaml");

        return page.GetVisualDescendants()
            .OfType<Tabs>()
            .Single(tabs => tabs.Items.OfType<TabItem>().Count() == 3 && Text(tabs, 2) == markup);
    }

    /// <summary>The text one tab of a block shows, or null when it is not a tab of text.</summary>
    /// <param name="tabs">The block's tabs.</param>
    /// <param name="at">Which tab.</param>
    /// <returns>The text.</returns>
    private static string? Text(Tabs tabs, int at) =>
        tabs.Items.OfType<TabItem>().ElementAt(at).Content is ScrollViewer { Content: SelectableTextBlock text }
            ? text.Text
            : null;

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
    private static string File(string name, string extension)
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
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
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
