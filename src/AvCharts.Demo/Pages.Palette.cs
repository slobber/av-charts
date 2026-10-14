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

        return Page(
            "Palette",
            "The ten colours, with the same data drawn light and dark.",
            Example("The palette in both variants", page),
            Try("Try it: the palette, one series at a time", PalettePlayground()));
    }

    /// <summary>
    /// One chart wearing the palette, with knobs for how much of it is on show and whether it is filled.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// A palette is an answer to "what does the eleventh series look like", so the knobs are how many series there
    /// are - the colours cycle when they run out - and whether each one has the fill that matches its stroke.
    /// </remarks>
    private static Control PalettePlayground()
    {
        var chart = Chart(0, 12, 0, 10);
        var series = 10;
        var areas = false;

        void Draw()
        {
            chart.Series.Clear();

            for (var slot = 1; slot <= series; slot++)
            {
                var values = new double[13];

                for (var index = 0; index < values.Length; index++)
                {
                    values[index] = 5 + (4 * Math.Sin((index / 2.0) + (slot / 1.5)));
                }

                // Two shapes rather than one: a series with a line and a series with an area under it are the two
                // ways the palette's pair of tokens - the stroke and the tint that goes with it - are spent.
                chart.Series.Add(areas ? Area($"S{slot}", values) : Line($"S{slot}", values));
            }
        }

        Draw();

        var counts = new[] { 1, 3, 5, 10 };

        return Playground(
            chart,
            Choices("Series", ["1", "3", "5", "10"], 3, picked => { series = counts[picked]; Draw(); }),
            OnOff("Area under them", false, on => { areas = on; Draw(); }));
    }
}
