using Avalonia.Controls;
using Avalonia.Styling;
using AvCharts.Themes;

namespace AvCharts.Demo;

// The page of the palette, in both variants.
public static partial class Pages
{
    /// <summary>The ten colours of the palette, in both variants, side by side.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Palette()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };

        foreach (var (variant, column) in new[] { (ThemeVariant.Light, 0), (ThemeVariant.Dark, 2) })
        {
            var chart = Chart(0, 12, 0, 10);

            for (var slot = 1; slot <= ChartTokens.PaletteSize; slot++)
            {
                chart.Series.Add(Line($"S{slot}", [.. Enumerable.Range(0, 13).Select(index => 5 + (4 * Math.Sin((index / 2.0) + (slot / 1.5))))]));
            }

            // One scope per variant: each chart reads the tokens - and the palette - of the variant it is in.
            var scope = new ThemeVariantScope { RequestedThemeVariant = variant, Child = chart };

            Grid.SetColumn(scope, column);
            page.Children.Add(scope);
        }

        return Page("Palette", "The ten colours, with the same data drawn light and dark.", Example("The palette in both variants", page));
    }
}
