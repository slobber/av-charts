using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;

namespace AvCharts.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        Fill(this.FindControl<CartesianChart>("LightChart")!);
        Fill(this.FindControl<CartesianChart>("DarkChart")!);
    }

    /// <summary>
    /// Puts one line per palette slot on a chart: the colours come from the theme, so the same call draws the
    /// light palette or the dark one depending on the scope the chart is in.
    /// </summary>
    /// <param name="chart">The chart to fill.</param>
    private static void Fill(CartesianChart chart)
    {
        chart.HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 };
        chart.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 2 };

        for (var slot = 1; slot <= ChartTokens.PaletteSize; slot++)
        {
            chart.Series.Add(new LineSeries<double>
            {
                DisplayName = $"S{slot}",
                ItemsSource = Wave(slot),
                YSelector = value => value,
                StrokeThickness = 2,
            });
        }
    }

    /// <summary>A wave that starts a little further along for each series, so ten of them can be told apart.</summary>
    /// <param name="series">The slot of the series, from one.</param>
    /// <returns>The values, which stay inside the axes above.</returns>
    private static double[] Wave(int series) =>
        [.. Enumerable.Range(0, 13).Select(index => 5 + (4 * Math.Sin((index / 2.0) + (series / 1.5))))];
}
