using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo;

// The chart, the series and the readings that more than one page is drawn from: the vocabulary a page writes in,
// kept in one place because it belongs to no single page. What only one page needs lives in that page's file.
public static partial class Pages
{
    /// <summary>A chart with a window on it and nothing else.</summary>
    /// <param name="minimumX">The left of the horizontal window.</param>
    /// <param name="maximumX">The right of it.</param>
    /// <param name="minimumY">The bottom of the vertical window.</param>
    /// <param name="maximumY">The top of it.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(double minimumX, double maximumX, double minimumY, double maximumY) =>
        new()
        {
            HorizontalAxis = new LinearAxis { Minimum = minimumX, Maximum = maximumX },
            VerticalAxis = new LinearAxis { Minimum = minimumY, Maximum = maximumY },
        };

    /// <summary>A line of readings.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <param name="set">Anything else to set on it.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(
        string name,
        double[] values,
        Action<LineSeries<double>>? set = null)
    {
        var series = new LineSeries<double>
        {
            DisplayName = name,
            ItemsSource = values,
            YSelector = value => value,
            StrokeThickness = 2,
        };

        set?.Invoke(series);
        return series;
    }

    /// <summary>A line with the area under it filled.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static AreaSeries<double> Area(string name, double[] values)
    {
        var series = new AreaSeries<double>
        {
            DisplayName = name,
            ItemsSource = values,
            YSelector = value => value,
            StrokeThickness = 2,
        };

        // The area is filled with the colour the line is drawn in, which is what the palette slot gives it.
        series.Fill = series.Color is { } colour ? new SolidColorBrush(((ISolidColorBrush)colour).Color, 0.35) : null;
        return series;
    }

    /// <summary>A line that goes across and then up, rather than straight to the next reading.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <param name="filled">Whether the area under it is filled.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Steps(string name, double[] values, bool filled = false)
    {
        LineSeries<double> series = filled ? new StepAreaSeries<double>() : new StepLineSeries<double>();

        series.DisplayName = name;
        series.ItemsSource = values;
        series.YSelector = value => value;
        series.StrokeThickness = 2;

        if (series is StepAreaSeries<double> area && series.Color is { } colour)
        {
            area.Fill = new SolidColorBrush(((ISolidColorBrush)colour).Color, 0.35);
        }

        return series;
    }

    /// <summary>The same readings, lower down, so that a page of five lines can be read.</summary>
    /// <param name="values">The readings.</param>
    /// <param name="by">How much lower.</param>
    /// <returns>The readings.</returns>
    private static double[] Lower(double[] values, double by) => [.. values.Select(value => value - by)];

    /// <summary>A column of readings, one per slot.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static ColumnSeries<double> Column(string name, double[] values) =>
        new()
        {
            DisplayName = name,
            ItemsSource = values,
            YSelector = value => value,
        };

    /// <summary>Two years of readings against the names of things, which the bars page and its playground share.</summary>
    /// <param name="chart">The chart to put them on.</param>
    private static void TwoYears(CartesianChart chart)
    {
        chart.Series.Add(new BarSeries<Reading>
        {
            DisplayName = "This year",
            ItemsSource = [new("North", 6), new("South", 3), new("East", 7), new("West", 4), new("Middle", 5)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Name,
            Fill = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4)),
        });

        chart.Series.Add(new BarSeries<Reading>
        {
            DisplayName = "Last year",
            ItemsSource = [new("North", 3), new("South", 2), new("East", 4), new("West", 2), new("Middle", 3)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Name,
            Fill = new SolidColorBrush(Color.FromRgb(0x9E, 0xC5, 0xE8)),
        });
    }

    /// <summary>The scatter the points page and its playground share: a spread of readings with a bubble each.</summary>
    /// <returns>The readings.</returns>
    /// <remarks>
    /// Worked out rather than written out, so that the page is a spread rather than a list: every reading is
    /// somewhere else across, a value that walks a different way, and a bubble of its own size.
    /// </remarks>
    /// <remarks>
    /// Worked out rather than written out, and deliberately not in the order of their horizontal value: a scatter
    /// is a spread, and a reader who points at one of these is asking about that one.
    /// </remarks>
    private static Reading[] Scatter() =>
        [.. Enumerable.Range(0, 24)
            .Select(index => new Reading($"P{index}", 1 + ((index * 7) % 8), 0.5 + (index % 9), 2 + (index % 5)))];

    /// <summary>Puts a chart in a cell of a page, with a caption above it.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="column">Which column.</param>
    /// <param name="row">Which row.</param>
    /// <param name="caption">The caption.</param>
    /// <returns>The cell.</returns>
    private static Control Cell(Control chart, int column, int row, string caption)
    {
        var cell = new StackPanel { Margin = new Thickness(4) };

        // The chart is sized here rather than the cell: the stack panel in the cell would otherwise measure it
        // with unlimited height, and a chart answers that with a height of its own that overflows the cell.
        if (chart is CartesianChart sized && double.IsNaN(sized.Height))
        {
            sized.Height = CellHeight;
        }

        cell.Children.Add(new TextBlock { Text = caption, FontSize = 11, Opacity = 0.7 });
        cell.Children.Add(chart);

        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        return cell;
    }

    /// <summary>A reading with a name, the value charts stand it at, and the size of its bubble.</summary>
    /// <param name="Name">What it is called.</param>
    /// <param name="Value">The reading.</param>
    /// <param name="X">Where it is across, when a chart puts it somewhere.</param>
    /// <param name="Size">How far its bubble reaches, in pixels.</param>
    private sealed record Reading(string Name, double Value, double X = 0, double Size = 0);
}
