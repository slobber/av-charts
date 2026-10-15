using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using iPaas.Controls;

namespace AvCharts.Demo;

/// <summary>Which language the demo is speaking.</summary>
public enum DemoLanguage
{
    /// <summary>English, which is what the demo is written in and what it opens in.</summary>
    English,

    /// <summary>Chinese.</summary>
    Chinese,
}

/// <summary>
/// The demo's own words, in the languages it speaks.
/// </summary>
/// <remarks>
/// <para>
/// Two mechanisms rather than one, because the demo has two kinds of text. Every word a page is built out of is
/// a literal in a page's file, and those are translated when a page is put on screen - <see cref="Apply"/> walks
/// what was built and asks <see cref="Translations"/> for each word, so a page says nothing about languages at
/// all. The words a page *works out* - how many readings are arriving, how big the frame was dragged to - cannot
/// be looked up by their own text, and those are written with <see cref="Of"/> where they are written.
/// </para>
/// <para>
/// The rule for both is the same and it is the one thing to keep: a word that is also a *value* is never
/// translated. A knob's options are the strings its handler compares against, and a test looks for a row by the
/// name it was registered under - so the value stays as it is and only the display is translated, which is why an
/// option's words go through a template rather than through the list.
/// </para>
/// <para>
/// English is the demo's own language and translating it changes nothing: every lookup answers with the word it
/// was asked about. A page built in English is therefore exactly the page the golden images were recorded from.
/// </para>
/// </remarks>
public static class Text
{
    /// <summary>The language being shown; English until somebody picks otherwise.</summary>
    public static DemoLanguage Language { get; private set; } = DemoLanguage.English;

    /// <summary>Raised when the language changes, so that whatever is on screen can be said again.</summary>
    public static event EventHandler? Changed;

    /// <summary>Picks the language the demo speaks.</summary>
    /// <param name="language">The language.</param>
    public static void Speak(DemoLanguage language)
    {
        if (Language == language)
        {
            return;
        }

        Language = language;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>One of the demo's words, in the language being shown.</summary>
    /// <param name="english">The word in English.</param>
    /// <param name="chinese">The word in Chinese.</param>
    /// <returns>The word.</returns>
    /// <remarks>
    /// For the words a page works out rather than writes down, and for the two or three places a word is both a
    /// value and a label: everything else is translated by walking the page.
    /// </remarks>
    public static string Of(string english, string chinese) =>
        Language == DemoLanguage.Chinese ? chinese : english;

    /// <summary>One of the demo's words, translated; the English itself when there is no translation yet.</summary>
    /// <param name="english">The word in English.</param>
    /// <returns>The word in the language being shown.</returns>
    public static string Say(string english) =>
        Language == DemoLanguage.Chinese && Translations.Chinese.TryGetValue(english, out var chinese)
            ? chinese
            : english;

    /// <summary>Says everything on a page in the language being shown.</summary>
    /// <param name="page">The page, or any control.</param>
    /// <remarks>
    /// Walked rather than bound, and that is the trade this demo made: its pages are built in code, so a word is
    /// a literal where it is written and there is nothing for a binding to sit on. Walking finds every one of
    /// them in one place instead of asking every page to say what it has - and a page that grows a caption
    /// tomorrow is translated by that caption's being there.
    /// </remarks>
    public static void Apply(Control page)
    {
        Said(page, page);

        foreach (var child in page.GetVisualDescendants().OfType<Control>())
        {
            Said(child, page);
        }
    }

    /// <summary>One word of the demo's, said in the language being shown.</summary>
    /// <param name="words">The word, or nothing.</param>
    /// <returns>The word, or nothing.</returns>
    private static string? Said(string? words) => words is { Length: > 0 } ? Say(words) : words;

    /// <summary>Says one control's words in the language being shown.</summary>
    /// <param name="control">The control.</param>
    /// <param name="page">The page it is on, which is where a block's own words are.</param>
    private static void Said(Control control, Control page)
    {
        switch (control)
        {
            case TextBlock text when text.Text is { Length: > 0 } words:
                text.Text = Say(words);
                break;

            // A block's title and line are properties rather than text, and a page's too.
            case DemoBlock block:
                block.Title = Said(block.Title);
                block.Description = Said(block.Description);
                break;

            case DemoPage shown:
                shown.Title = Said(shown.Title);
                shown.Description = Said(shown.Description);
                break;

            // A tab's header is a word of the shell's rather than a value, so it is said outright.
            case TabItem tab when tab.Header is string header:
                tab.Header = Say(header);
                break;

            // An option's words are the value the page compares against, so only the way it is written is
            // translated: the list keeps what it holds and a template says it.
            case SelectingItemsControl picker when Language != DemoLanguage.English:
                picker.ItemTemplate = new FuncDataTemplate<object>((item, _) =>
                    new TextBlock { Text = item is string word ? Say(word) : item?.ToString() });
                break;

            case Toggle toggle when Language != DemoLanguage.English && toggle.Content is string word:
                toggle.ContentTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock { Text = Say(word) });
                break;

            default:
                break;
        }

        _ = page;
    }

    /// <summary>The words for a page's name and its line, which the shell shows above the page.</summary>
    /// <param name="entry">The page.</param>
    /// <returns>The name and the line, in the language being shown.</returns>
    public static (string Title, string About) Entry(DemoEntry entry) =>
        (Say(entry.Title), Say(entry.Description));
}
