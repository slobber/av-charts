using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var chart = this.FindControl<CartesianChart>("Chart")!;

        chart.HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 };
        chart.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 2 };

        // The axes, the grid and the labels are painted in the theme's inks: the control theme sets them from
        // the tokens in Application.Resources, and the chart hands them to its layers.
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
    }
}
