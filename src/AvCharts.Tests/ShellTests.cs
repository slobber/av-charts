using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvCharts.Demo;
using iPaas.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the shell the demo is shown in: the catalogue on the left, the page on the right.
/// </summary>
/// <remarks>
/// <para>
/// The shell is chrome rather than chart, but it has had a bug of its own: the rows of the catalogue were built
/// out of plain buttons and only half-dressed, so their labels were drawn in the primary button's foreground -
/// which is white - on a page that is very nearly white. A picture is what catches that, and a count of the ink
/// says which colour the label came out in, so the shell gets both.
/// </para>
/// <para>
/// The shell is built here as the browser builds it - a control on its own, not a window's content - because
/// that is what the page for the published gallery is handed, and it is the only head whose appearance nobody
/// can see in a test. Anything the shell takes from a window it does not have (its styles, the theme in its
/// resources) therefore has to be the shell's own, and these pictures say whether it is: a shell that lost its
/// theme would draw a gallery of undressed charts, which the golden image refuses.
/// </para>
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ShellTests
{
    [GoldenFact]
    public void The_catalogue_is_drawn_in_the_colour_text_is()
    {
        var shell = new Shell();

        // Shown, so that the shell is laid out the way the application lays it out: a tree with no root is a
        // tree whose templates a manual measure does not always finish - the two scroll views of the shell are
        // the case in point.
        Show(shell);

        shell.Measure(new Size(1180, 820));
        shell.Arrange(new Rect(0, 0, 1180, 820));

        // A row of a page rather than the brand's row: the brand is a label in the header, and the rows of the
        // catalogue are the ones that went white.
        var row = shell.GetVisualDescendants().OfType<SidebarMenuButton>().First(one => (one.Label as string) == "Lines");
        var label = row.GetVisualDescendants().OfType<TextBlock>().First();
        var at = label.TranslatePoint(new Point(), shell);

        Assert.NotNull(at);
        Assert.True(label.Bounds.Width > 0, "the row has nothing written on it");

        var written = new Rect(at!.Value, label.Bounds.Size);
        var picture = Picture(shell);

        // The colour the theme says text is, and room for antialiasing: a label drawn in white - the primary
        // button's foreground - is a label nobody can read on this page, and this is what says so.
        Assert.True(
            Ink.Count(picture, TextColour(), written, 40) > 0,
            $"the catalogue's labels are not drawn in the colour text is ({written} of the shell)");

        GoldenImages.Verify("demo-shell", shell, 1180, 820);
    }

    [Fact]
    public void Every_word_the_gallery_says_in_chinese_can_be_drawn()
    {
        // The gallery speaks two languages and the font that draws the second one travels inside it, because a
        // browser brings no fonts of its own to fall back on: a gallery that ships one font is a gallery of empty
        // boxes as soon as a reader picks the other language, which is what the published one was until this font
        // was added. The face is asked directly rather than through the font manager, so that the test means the
        // same thing on a machine whose operating system has Chinese fonts of its own - where the boxes would never
        // appear and the bug would hide.
        var manager = FontManager.Current;

        // The Chinese font is asked by the address the gallery names it by: one that is not there at all quietly
        // becomes whatever the platform has, and a Chinese page would go on being a page of boxes.
        Assert.True(manager.TryGetGlyphTypeface(new Typeface(new FontFamily(DemoFonts.ChineseFamily)), out var chinese));
        Assert.Equal("AvCharts Chinese", chinese.FamilyName);

        // And what draws a character is the font the application was dressed in, not whatever the machine happens to
        // have: the demo's fallback is consulted before the platform's, so a character it is missing shows up here as
        // a character drawn in somebody else's face - which is exactly the box on the screen, one step earlier.
        var missing = new List<char>();

        foreach (var character in Translations.Chinese.Values.Concat(Translations.Languages).SelectMany(word => word))
        {
            if (character <= '\u007f')
            {
                continue;
            }

            if (!manager.TryMatchCharacter(
                    character,
                    FontStyle.Normal,
                    FontWeight.Normal,
                    FontStretch.Normal,
                    null,
                    CultureInfo.InvariantCulture,
                    out var matched)
                || matched.GlyphTypeface.FamilyName != "AvCharts Chinese")
            {
                missing.Add(character);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"the gallery cannot draw {missing.Distinct().Count()} of the characters it writes in Chinese: "
            + string.Join("", missing.Distinct()));
    }

    [Fact]
    public void Choosing_a_row_shows_that_page_and_marks_the_row()
    {
        var shell = new Shell();

        Show(shell);

        shell.Measure(new Size(1180, 820));
        shell.Arrange(new Rect(0, 0, 1180, 820));

        var rows = shell.GetVisualDescendants().OfType<SidebarMenuButton>().ToArray();
        var lines = rows.First(one => (one.Label as string) == "Lines");

        lines.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, lines));

        Assert.Equal("Lines", shell.GetVisualDescendants().OfType<TextBlock>().First(one => one.Name == "CurrentPage").Text);
        Assert.True(lines.IsActive, "the row of the page being shown is not marked");
        Assert.Single(rows, one => one.IsActive);
    }

    [Theory]
    [InlineData(DemoLanguage.English, "https://nandi.wang/av-charts/docs/en/using/index.html")]
    [InlineData(DemoLanguage.Chinese, "https://nandi.wang/av-charts/docs/using/index.html")]
    public void The_brand_leads_to_the_guide_in_the_language_being_shown(DemoLanguage language, string address)
    {
        Assert.Equal(new Uri(address), Shell.DocumentationHome(language));
    }

    [Fact]
    public void The_settings_at_the_foot_say_the_demo_in_another_language_and_wear_the_dark_theme()
    {
        var shell = new Shell();
        var was = Application.Current?.RequestedThemeVariant;

        Show(shell);

        shell.Measure(new Size(1180, 820));
        shell.Arrange(new Rect(0, 0, 1180, 820));

        try
        {
            var settings = shell.GetVisualDescendants().OfType<StackPanel>().Single(one => one.Name == "SettingsPanel");
            var language = settings.GetVisualDescendants().OfType<Select>().Single();
            var dark = settings.GetVisualDescendants().OfType<Switch>().Single();

            // The two settings are at the foot of the catalogue, below every row of it - which is where a reader
            // looks for the language and the theme rather than among the pages.
            var navigation = shell.GetVisualDescendants().OfType<StackPanel>().Single(one => one.Name == "Navigation");
            var foot = settings.TranslatePoint(new Point(), shell);
            var rows = navigation.TranslatePoint(new Point(), shell);

            Assert.NotNull(foot);
            Assert.NotNull(rows);
            Assert.True(foot!.Value.Y > rows!.Value.Y, $"the settings at {foot} sit above the catalogue at {rows}");

            Assert.Equal(new object[] { "English", "中文" }, language.ItemsSource!.Cast<object>().ToArray());

            // Picking the other language says the catalogue, the page's own name above it, and the page itself;
            // the option values stay English, because they are what a page's handler compares against.
            language.SelectedItem = "中文";

            shell.Measure(new Size(1180, 820));
            shell.Arrange(new Rect(0, 0, 1180, 820));

            Assert.Equal(
                "一页看全",
                shell.GetVisualDescendants().OfType<TextBlock>().First(one => one.Name == "CurrentPage").Text);
            Assert.Contains(
                shell.GetVisualDescendants().OfType<SidebarMenuButton>(),
                row => (row.Label as string) == "折线");

            // And the switch wears the theme the whole application wears, not one chart's.
            dark.IsChecked = true;

            Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);
        }
        finally
        {
            // Both of these are the process's rather than the test's, so they are put back: a language left in
            // Chinese would have every picture recorded afterwards in Chinese.
            Text.Speak(DemoLanguage.English);

            if (Application.Current is { } application)
            {
                application.RequestedThemeVariant = was;
            }
        }
    }

    /// <summary>Puts the shell in a window of its own, so that it is laid out the way an application lays it out.</summary>
    /// <param name="shell">The shell to show.</param>
    /// <remarks>
    /// The window is the test's host and nothing more: what is being shown is the control, which is what a
    /// browser is given - there is no window there at all, and asking for one throws.
    /// </remarks>
    private static void Show(Shell shell)
    {
        var host = new Window { Width = 1180, Height = 820, Content = shell };

        host.Show();
    }

    /// <summary>Draws the shell, so that the ink in it can be counted.</summary>
    /// <param name="shell">The shell, as the browser is given it.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(Control shell)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(1180, 820));

        bitmap.Render(shell);
        return bitmap;
    }

    /// <summary>The colour the house's theme draws text in, which is what a label has to be.</summary>
    /// <returns>The colour.</returns>
    private static Color TextColour()
    {
        Assert.True(
            Application.Current!.TryGetResource("ForegroundBrush", ThemeVariant.Light, out var value),
            "the theme has no ForegroundBrush to read text in");

        return ((ISolidColorBrush)value!).Color;
    }
}
