using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using AvCharts.Themes;

namespace AvCharts.Demo;

/// <summary>
/// The demo: the catalogue on the left, the chosen page on the right, and the page's name and line above it.
/// </summary>
/// <remarks>
/// The navigation is built out of <see cref="Catalog"/>, which is the one place a page is registered, and the
/// pages themselves come out of <see cref="Pages"/> - so the demo is a shell around two lists that the golden
/// tests walk as well.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly Dictionary<Button, DemoEntry> _rows = [];

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // The theme goes into the window's resources, which is how a host dresses every chart it has: one line,
        // and each chart takes its control theme from the resources it can see.
        Resources = ChartTheme.Create();

        BuildNavigation();
        Show(Catalog.Groups[0].Entries[0]);
    }

    /// <summary>Builds the catalogue: a heading per group, and a row per page under it.</summary>
    private void BuildNavigation()
    {
        var navigation = this.FindControl<StackPanel>("Navigation")!;

        foreach (var group in Catalog.Groups)
        {
            navigation.Children.Add(new TextBlock
            {
                Text = group.Title,
                FontSize = 11,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Opacity = 0.6,
                Margin = new Thickness(8, 12, 0, 0),
            });

            foreach (var entry in group.Entries)
            {
                var row = new Button
                {
                    Content = entry.Title,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = default,
                    Padding = new Thickness(8, 6),
                };

                row.Click += (_, _) => Show(entry);

                _rows.Add(row, entry);
                navigation.Children.Add(row);
            }
        }
    }

    /// <summary>Shows a page, and says so above it.</summary>
    /// <param name="entry">The page to show.</param>
    private void Show(DemoEntry entry)
    {
        this.FindControl<ContentControl>("PageHost")!.Content = entry.Create();
        this.FindControl<TextBlock>("CurrentPage")!.Text = entry.Title;
        this.FindControl<TextBlock>("CurrentAbout")!.Text = entry.Description;

        foreach (var (row, page) in _rows)
        {
            row.FontWeight = ReferenceEquals(page, entry) ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal;
        }
    }
}
