using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using AvCharts.Controls;
using AvCharts.Demo;
using AvCharts.Themes;
using iPaas.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks that the gallery says everything it says in both languages.
/// </summary>
/// <remarks>
/// The demo translates what a reader reads by looking the words up in <see cref="Translations.Chinese"/> as the
/// page goes up, so a word nobody wrote down stays English - which is the right behaviour for a word that has not
/// been translated yet and the wrong one for a page that is offered as speaking Chinese. This is the check that
/// tells the two apart: every word a page shows has to be in the table, except what is not a word.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class TranslationTests
{
    /// <summary>
    /// What a page may show in English while the rest of it is Chinese: numbers and units, which read the same either
    /// way; the names of files; and the names of the readings a knob points at, which are the example's data - the
    /// option that chooses between millimetres and inches says the same thing in either language, the way the legend
    /// of that chart does.
    /// </summary>
    private static readonly string[] SameInBothLanguages = ["axaml", "millimetres", "inches"];

    [Fact]
    public void Every_word_a_page_shows_is_in_the_table()
    {
        var missing = new List<string>();

        foreach (var entry in Catalog.Groups.SelectMany(group => group.Entries))
        {
            var page = Show(entry.Title);

            page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
            page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

            // What a chart draws is the example's own - axes, legends, tips, the readings it is drawn from - and none
            // of it is the demo's to translate: a chart of sales says North and South in either language.
            var written = page.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(one => !one.GetVisualAncestors().Any(one => one is ChartBase or Legend or LegendItem))
                .Select(one => one.Text)
                .Where(one => !string.IsNullOrWhiteSpace(one));

            // The options of a knob as well as what is showing: a picker shows the one it is on and keeps the rest
            // until it is opened, and a word standing in a list a reader is about to open is a word they read.
            var offered = page.GetVisualDescendants()
                .SelectMany(one => one switch
                {
                    Select picker => (picker.ItemsSource as System.Collections.IEnumerable ?? Array.Empty<object>())
                        .Cast<object>()
                        .Select(option => option?.ToString() ?? string.Empty),
                    Toggle button => [button.Content?.ToString() ?? string.Empty],
                    // A label and nothing else: the buttons a control's own template is made of have no words of
                    // their own, and a word that is not there is not one the demo owes a translation for.
                    Avalonia.Controls.Button button when button.Content is string label => [label],
                    _ => Array.Empty<string>(),
                });

            foreach (var written_ in written.Concat(offered))
            {
                var word = written_!;

                if (word.Any(char.IsDigit) || SameInBothLanguages.Contains(word))
                {
                    continue;
                }

                if (!Translations.Chinese.ContainsKey(word))
                {
                    missing.Add($"{entry.Title}: {word}");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} words are shown in English whichever language is chosen: "
            + string.Join("; ", missing.Distinct().Order()));
    }

    [Fact]
    public void A_page_that_is_built_in_chinese_says_its_own_words_in_chinese()
    {
        // Which is the whole of the way the demo speaks two languages: a page is built out of its words and then
        // told which language it is in - and it is told *before* it goes on screen, so the telling has to reach
        // what it is made of rather than what has been laid out. It did not, and the pages said their headings and
        // their knobs in English however the language was set.
        var was = Text.Language;

        try
        {
            Text.Speak(DemoLanguage.Chinese);

            var page = Catalog.Groups
                .SelectMany(group => group.Entries)
                .Single(entry => entry.Title == "Axes")
                .Create();

            Text.Apply(page);

            // Never measured, never arranged, never in a window: what a page says when it is built and told.
            Assert.Equal("坐标轴", Assert.IsType<DemoPage>(page).Title);

            var blocks = page.GetLogicalDescendants().OfType<DemoBlock>().Select(one => one.Title).ToArray();

            Assert.NotEmpty(blocks);
            Assert.All(blocks, title => Assert.Matches("^[^A-Za-z]*$", title ?? string.Empty));

            var knobs = page.GetLogicalDescendants()
                .OfType<TextBlock>()
                .Select(one => one.Text ?? string.Empty)
                .Where(one => one.Length > 0)
                .ToArray();

            Assert.Contains("横轴", knobs);
        }
        finally
        {
            Text.Speak(was);
        }
    }

    /// <summary>Shows a page the way the demo window shows it, without a window of its own.</summary>
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
