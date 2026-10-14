using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvCharts.Themes;
using iPaas.Controls;
using Lucide.Avalonia;

namespace AvCharts.Demo;

/// <summary>
/// The demo: the catalogue on the left, the chosen page on the right, and the page's name and line above it.
/// </summary>
/// <remarks>
/// The navigation is built out of <see cref="Catalog"/>, which is the one place a page is registered, and the
/// pages themselves come out of <see cref="Gallery"/> - so the demo is a shell around two lists that the golden
/// tests walk as well.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly Dictionary<SidebarMenuButton, DemoEntry> _rows = [];

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // The theme goes into the window's resources, which is how a host dresses every chart it has: one line,
        // and each chart takes its control theme from the resources it can see.
        Resources = ChartTheme.Create();

        // The trigger and the rail both fold the catalogue; the trigger needs to be told which panel it is for.
        this.FindControl<SidebarTrigger>("CatalogueTrigger")!.Target = this.FindControl<Sidebar>("Catalogue");

        BuildNavigation();
        Show(Catalog.Groups[0].Entries[0]);
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

        foreach (var group in Catalog.Groups)
        {
            var menu = new SidebarMenu();

            foreach (var entry in group.Entries)
            {
                var row = new SidebarMenuButton
                {
                    Label = entry.Title,
                    Icon = Glyph(entry.Icon),
                };

                row.Click += (_, _) => Show(entry);

                _rows.Add(row, entry);
                menu.Children.Add(new SidebarMenuItem { Children = { row } });
            }

            navigation.Children.Add(new SidebarGroup
            {
                Children =
                {
                    new SidebarGroupLabel { Content = group.Title },
                    menu,
                },
            });
        }
    }

    /// <summary>Shows a page, marks its row, and says what it is above the page.</summary>
    /// <param name="entry">The page to show.</param>
    private void Show(DemoEntry entry)
    {
        this.FindControl<ContentControl>("PageHost")!.Content = entry.Create();
        this.FindControl<TextBlock>("CurrentPage")!.Text = entry.Title;
        this.FindControl<TextBlock>("CurrentAbout")!.Text = entry.Description;

        foreach (var (row, page) in _rows)
        {
            row.IsActive = ReferenceEquals(page, entry);
        }
    }

    /// <summary>One row's glyph, at the weight the house draws them at.</summary>
    /// <param name="kind">Which glyph.</param>
    /// <returns>The icon.</returns>
    private static LucideIcon Glyph(LucideIconKind kind) => new() { Kind = kind, Size = 16, StrokeWidth = 1.5 };
}
