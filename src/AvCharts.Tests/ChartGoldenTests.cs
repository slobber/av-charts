using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

[Collection(AvaloniaCollection.Name)]
public class ChartGoldenTests
{
    [Fact]
    public void The_first_line_chart()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 2 },
        };

        chart.HorizontalLayer.LabelBrush = Brushes.Black;
        chart.VerticalLayer.LabelBrush = Brushes.Black;
        chart.VerticalLayer.GridLinePen = new Pen(Brushes.LightGray, 1);
        chart.HorizontalLayer.AxisLinePen = new Pen(Brushes.Gray, 1);
        chart.VerticalLayer.AxisLinePen = new Pen(Brushes.Gray, 1);

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = [1.0, 3.5, 2.0, 6.0, 4.5, 8.5, 7.0, 9.5, 6.5, 9.0, 10.0],
            YSelector = value => value,
            Stroke = Brushes.OrangeRed,
            StrokeThickness = 2,
            Marker = Markers.Circle,
            MarkerSize = 3,
            MarkerFill = Brushes.White,
        });

        GoldenImages.Verify("first-line-chart", chart, 400, 200);
    }

    [Fact]
    public void The_palette_in_both_variants()
    {
        // The gallery's comparison page as a picture the build checks: one window, two variants, the same ten
        // lines drawn through the palette of each. It is also the only place a machine can look at the two
        // palettes together, since the gallery itself is not something the tests can run.
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Resources = ChartTheme.Create() };

        foreach (var (variant, column) in new[] { (ThemeVariant.Light, 0), (ThemeVariant.Dark, 1) })
        {
            var chart = new CartesianChart
            {
                HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 12, Interval = 3 },
                VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 2 },
            };

            for (var slot = 1; slot <= ChartTokens.PaletteSize; slot++)
            {
                chart.Series.Add(new LineSeries<double>
                {
                    DisplayName = $"S{slot}",
                    ItemsSource = [.. Enumerable.Range(0, 13).Select(index => 5 + (4 * Math.Sin((index / 2.0) + (slot / 1.5))))],
                    YSelector = value => value,
                    StrokeThickness = 2,
                });
            }

            var scope = new ThemeVariantScope { RequestedThemeVariant = variant, Child = chart };
            Grid.SetColumn(scope, column);
            page.Children.Add(scope);
        }

        // The page needs a logical root for the charts to join, which is how a gallery puts one up.
        var window = new Window { Content = page };

        GoldenImages.Verify("palette-light-dark", page, 800, 300);
    }
}
