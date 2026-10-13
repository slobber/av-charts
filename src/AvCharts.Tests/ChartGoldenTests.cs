using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Controls;
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
}
