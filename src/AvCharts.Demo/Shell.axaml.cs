using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Themes;
using iPaas.Controls;

namespace AvCharts.Demo;

/// <summary>
/// The demo: the catalogue on the left, the chosen page on the right, and the page's name and line above it.
/// </summary>
/// <remarks>
/// A control rather than a window, because a head may have no window to put it in: the browser has one view
/// and no title bar, and asking Avalonia's browser backend for a window is a `NotSupportedException` there.
/// Both heads therefore build the same shell - the desktop hands it to <see cref="MainWindow"/>, which is a
/// frame around it, and the browser hands it to the single view - so a page cannot come out one way in a
/// browser and another way on the desktop, and the tests which walk the shell walk the browser's own.
///
/// The navigation is built out of <see cref="Catalog"/>, which is the one place a page is registered, and the
/// pages themselves come out of <see cref="Gallery"/> - so the demo is a shell around two lists that the golden
/// tests walk as well.
/// </remarks>
public partial class Shell : UserControl
{
    /// <summary>The size of a row's glyph, in the house's 24-unit icon box scaled down to it.</summary>
    private const double GlyphSize = 16;

    /// <summary>The stroke a glyph is drawn with; the house's chrome uses the same hairline.</summary>
    private const double GlyphStroke = 1.5;

    private readonly Dictionary<SidebarMenuButton, DemoEntry> _rows = [];

    /// <summary>The page being shown, so that a change of language can say it again.</summary>
    private DemoEntry _showing = Catalog.Groups[0].Entries[0];

    public Shell()
    {
        AvaloniaXamlLoader.Load(this);

        // The theme goes into the shell's resources, which is how a host dresses every chart it has: one line,
        // and each chart takes its control theme from the resources it can see.
        Resources = ChartTheme.Create();

        // The trigger and the rail both fold the catalogue; the trigger needs to be told which panel it is for.
        this.FindControl<SidebarTrigger>("CatalogueTrigger")!.Target = this.FindControl<Sidebar>("Catalogue");

        var brand = this.FindControl<SidebarMenuButton>("Brand")!;

        brand.Icon = Icon(brand, Icons.ChartSpline);

        BuildSettings();
        BuildNavigation();
        Show(Catalog.Groups[0].Entries[0]);

        // A change of language says the whole shell again: the catalogue's rows, the settings themselves, and the
        // page - which is rebuilt rather than relabelled, because a page is built out of its words.
        Text.Changed += (_, _) =>
        {
            BuildNavigation();
            BuildSettings();
            Show(_showing);
        };
    }

    /// <summary>
    /// Builds the catalogue: a heading per group, and a row per page under it.
    /// </summary>
    /// <remarks>
    /// Out of the house's own sidebar parts, so that a row of it is a row of it: it knows its own foreground,
    /// lights up under the pointer, and keeps an icon to fall back to when the panel is folded to its icons.
    /// </remarks>
    private void BuildNavigation()
    {
        var navigation = this.FindControl<StackPanel>("Navigation")!;

        navigation.Children.Clear();
        _rows.Clear();

        foreach (var group in Catalog.Groups)
        {
            var menu = new SidebarMenu();

            foreach (var entry in group.Entries)
            {
                var row = new SidebarMenuButton { Label = Text.Say(entry.Title) };

                // The icon goes in after the row exists, because it is painted in that row's foreground.
                row.Icon = Icon(row, entry.Icon);
                row.Click += (_, _) => Show(entry);

                _rows.Add(row, entry);
                menu.Children.Add(new SidebarMenuItem { Children = { row } });
            }

            navigation.Children.Add(new SidebarGroup
            {
                // The group's name and every row's come through the language being shown.
                Children =
                {
                    new SidebarGroupLabel { Content = Text.Say(group.Title) },
                    menu,
                },
            });
        }
    }

    /// <summary>Shows a page, marks its row, and says what it is above the page.</summary>
    /// <param name="entry">The page to show.</param>
    private void Show(DemoEntry entry)
    {
        _showing = entry;

        var (title, about) = Text.Entry(entry);
        var shown = entry.Create();

        // A page is built in the demo's own language and then told which one it is being shown in: its words are
        // literals where they were written, and this is the one place that knows there are two languages at all.
        Text.Apply(shown);

        this.FindControl<ContentControl>("PageHost")!.Content = shown;
        this.FindControl<TextBlock>("CurrentPage")!.Text = title;
        this.FindControl<TextBlock>("CurrentAbout")!.Text = about;

        foreach (var (row, page) in _rows)
        {
            row.IsActive = ReferenceEquals(page, entry);
        }
    }

    /// <summary>
    /// Builds the two settings at the foot of the catalogue: which theme the application wears, and which
    /// language the demo speaks.
    /// </summary>
    /// <remarks>
    /// Both are the application's rather than a chart's - the variant is set on the application so that the chrome
    /// and every chart in it change together, and the language is the demo's own. Built rather than bound, like
    /// the rest of the demo, and built again whenever the language changes so that the two settings' own words
    /// are said in the language a reader has just picked.
    /// </remarks>
    private void BuildSettings()
    {
        var panel = this.FindControl<StackPanel>("SettingsPanel")!;

        panel.Children.Clear();

        var dark = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;

        // The house's own switch and its own select, so that the foot of the catalogue is made of the same
        // controls as the knobs on a page: a reader who has used one has used the other.
        panel.Children.Add(Gallery.OnOff("Dark theme", dark, Wearing));

        var language = new Select
        {
            ItemsSource = Languages,
            SelectedItem = Text.Language == DemoLanguage.Chinese ? Languages[1] : Languages[0],
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        };

        language.SelectionChanged += (_, _) => Text.Speak(
            ReferenceEquals(language.SelectedItem, Languages[1]) ? DemoLanguage.Chinese : DemoLanguage.English);

        panel.Children.Add(Gallery.Labelled("Language", language));

        // The two settings' own words are the demo's words like any other, so they go through the same walk.
        Text.Apply(panel);
    }

    /// <summary>Puts the whole application in one variant or the other.</summary>
    /// <param name="dark">Whether it wears the dark one.</param>
    /// <remarks>
    /// The application's variant rather than the shell's: the chrome, the pages and every chart in them take
    /// their colours from the resources that variant picks, and a demo whose chart changed while its frame did
    /// not would be showing what the tokens can do rather than what the theme does.
    /// </remarks>
    private static void Wearing(bool dark)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    /// <summary>The two languages, each written in its own words rather than in the demo's.</summary>
    private static readonly string[] Languages = ["English", "中文"];

    /// <summary>One row's glyph, drawn in the colour the row writes its text in.</summary>
    /// <param name="row">The row the glyph belongs to, which is what it takes its colour from.</param>
    /// <param name="path">The glyph's path, out of <see cref="Icons"/>.</param>
    /// <returns>The icon.</returns>
    /// <remarks>
    /// A row that is the page being shown, or one the pointer is over, writes its text in another colour - and
    /// the glyph has to go with it, which is why the stroke is bound to that row's foreground rather than set
    /// to a colour of its own. The path is a string until here on purpose: the catalogue is read while the
    /// tests are being discovered, and a geometry built there would start a render loop on the thread doing the
    /// discovering (see <see cref="Icons"/>).
    /// </remarks>
    private static Glyph Icon(Control row, string path)
    {
        var glyph = new Glyph
        {
            Data = Geometry.Parse(path),
            Width = GlyphSize,
            Height = GlyphSize,
            StrokeThickness = GlyphStroke,
        };

        glyph.Bind(Glyph.StrokeProperty, new Binding(nameof(ContentControl.Foreground)) { Source = row });

        return glyph;
    }
}
