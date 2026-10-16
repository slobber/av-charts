using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Statistics page: an interval, an error bar and a box - three ways of drawing what a sample of readings says
// about itself - and the playground that draws one set of readings as any of the three.
internal static class StatisticsPage
{
    /// <summary>A range, an error bar and a box, one to a cell.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };

        page.Children.Add(Cell(Temperatures(), 0, 0, "Range"));
        page.Children.Add(Cell(Measurements(), 1, 0, "Error bars"));
        page.Children.Add(Cell(Samples(), 2, 0, "Boxes"));

        return Page(
            "Statistics",
            "An interval, an error bar and a box: what a sample of readings says about itself, drawn.",
            Sourced("Boxes drawn from five numbers a week", "Statistics/BoxesFromFiveNumbers"),
            Try("Try it: the same readings, as any of the three shapes", StatisticsPlayground()));
    }

    /// <summary>A week of temperatures, as the low and the high of each day.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Temperatures()
    {
        var chart = Plot();

        chart.Series.Add(
            new RangeSeries<Day>
            {
                DisplayName = "Low to high",
                ItemsSource = Days(),
                XSelector = day => day.Name,
                LowSelector = day => day.Low,
                HighSelector = day => day.High,
            });

        return chart;
    }

    /// <summary>Six measurements, each with the uncertainty it was taken to.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Measurements()
    {
        var chart = Plot();

        chart.Series.Add(
            new ErrorSeries<Station>
            {
                DisplayName = "Measured",
                ItemsSource = Stations(),
                XSelector = station => station.Name,
                YSelector = station => station.Value,
                ErrorSelector = station => station.Error,
            });

        return chart;
    }

    /// <summary>Six weeks of samples, each summarised by the five numbers a box is drawn from.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Samples()
    {
        var chart = Plot();

        chart.Series.Add(
            new BoxSeries<Week>
            {
                DisplayName = "Samples a week",
                ItemsSource = Weeks(),
                XSelector = week => week.Name,
                MinimumSelector = week => week.Minimum,
                LowerQuartileSelector = week => week.LowerQuartile,
                MedianSelector = week => week.Median,
                UpperQuartileSelector = week => week.UpperQuartile,
                MaximumSelector = week => week.Maximum,
            });

        return chart;
    }

    /// <summary>
    /// The same readings as any of the three shapes, with the band they stand in for a knob.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// One set of five numbers to a record - the smallest, two quartiles, the middle and the largest - is enough
    /// to draw all three, which is the thing worth showing: what a range, an error bar and a box each do with the
    /// same sample. The shape knob rebuilds the series in the chart, and the other two are the settings all three
    /// of them share - a band width and a cap - which is where the family lives.
    /// </remarks>
    private static Control StatisticsPlayground()
    {
        var weeks = Weeks();
        var chart = Plot();
        var shapes = new[] { "a range", "error bars", "boxes" };
        var widths = new[] { "40%", "65%", "90%" };
        var ratios = new[] { 0.4, 0.65, 0.9 };
        var caps = new double?[] { null, 24 };
        var current = Shape(shapes[0], weeks);

        chart.Series.Add(current);

        void Redraw(string picked)
        {
            // Rebuilt rather than reconfigured: the three are different series, and a knob that changed one into
            // another in place would be a knob whose setting went with the shape.
            chart.Series.Clear();
            chart.Series.Add(current = Shape(picked, weeks));
        }

        return Playground(
            chart,
            Choice("Shape", shapes, shapes[0], Redraw),
            Choice("Band width", widths, widths[1], picked => current.BandWidthRatio = ratios[Array.IndexOf(widths, picked)]),
            Choices("Widest", ["no cap", "24 px"], 0, picked => current.MaxBandWidth = caps[picked]));
    }

    /// <summary>One of the three shapes, over the same readings.</summary>
    /// <param name="shape">Which shape.</param>
    /// <param name="weeks">The readings.</param>
    /// <returns>The series.</returns>
    private static BandedSeries<Week> Shape(string shape, Week[] weeks) => shape switch
    {
        "a range" => new RangeSeries<Week>
        {
            DisplayName = "Smallest to largest",
            ItemsSource = weeks,
            XSelector = week => week.Name,
            LowSelector = week => week.Minimum,
            HighSelector = week => week.Maximum,
        },
        "error bars" => new ErrorSeries<Week>
        {
            DisplayName = "The middle, and how far it was worth",
            ItemsSource = weeks,
            XSelector = week => week.Name,
            YSelector = week => week.Median,
            ErrorSelector = week => (week.Maximum - week.Minimum) / 2,
        },
        _ => new BoxSeries<Week>
        {
            DisplayName = "The five numbers",
            ItemsSource = weeks,
            XSelector = week => week.Name,
            MinimumSelector = week => week.Minimum,
            LowerQuartileSelector = week => week.LowerQuartile,
            MedianSelector = week => week.Median,
            UpperQuartileSelector = week => week.UpperQuartile,
            MaximumSelector = week => week.Maximum,
        },
    };

    /// <summary>A chart of categories against readings, scaled to what is on it.</summary>
    /// <returns>The chart.</returns>
    /// <remarks>
    /// The value axis is left to cover the readings rather than pinned to a window: a range, an error bar and a
    /// box all reach between readings of their own, and an axis that insisted on zero would draw every one of
    /// them as a smear at the top of the plot.
    /// </remarks>
    private static CartesianChart Plot() =>
        new()
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis(),
        };

    /// <summary>One day: what it was called, and how cold and how warm it got.</summary>
    /// <param name="Name">The day.</param>
    /// <param name="Low">The coldest.</param>
    /// <param name="High">The warmest.</param>
    private sealed record Day(string Name, double Low, double High);

    /// <summary>One station: what it measured, and to how much of it.</summary>
    /// <param name="Name">The station.</param>
    /// <param name="Value">What it measured.</param>
    /// <param name="Error">How much either side of that.</param>
    private sealed record Station(string Name, double Value, double Error);

    /// <summary>One week of samples: the five numbers a box is drawn from.</summary>
    /// <param name="Name">The week.</param>
    /// <param name="Minimum">The smallest sample.</param>
    /// <param name="LowerQuartile">The lower quartile.</param>
    /// <param name="Median">The middle sample.</param>
    /// <param name="UpperQuartile">The upper quartile.</param>
    /// <param name="Maximum">The largest sample.</param>
    private sealed record Week(
        string Name,
        double Minimum,
        double LowerQuartile,
        double Median,
        double UpperQuartile,
        double Maximum);

    /// <summary>The week of temperatures the page opens with.</summary>
    /// <returns>The days, in order.</returns>
    private static Day[] Days() =>
    [
        new("Mon", 6.5, 14.2),
        new("Tue", 7.1, 16.4),
        new("Wed", 5.2, 13.1),
        new("Thu", 8.0, 18.6),
        new("Fri", 9.4, 20.2),
        new("Sat", 7.8, 17.5),
        new("Sun", 6.2, 15.3),
    ];

    /// <summary>The six measurements the page opens with.</summary>
    /// <returns>The stations, in order.</returns>
    private static Station[] Stations() =>
    [
        new("North", 12.4, 0.8),
        new("South", 13.1, 1.2),
        new("East", 11.6, 0.5),
        new("West", 12.9, 0.9),
        new("Centre", 13.4, 1.4),
        new("Coast", 11.2, 0.7),
    ];

    /// <summary>The weeks of samples the page and its playground are drawn from.</summary>
    /// <returns>The weeks, in order.</returns>
    /// <remarks>Written down rather than worked out: five numbers to a week is what a summary is.</remarks>
    private static Week[] Weeks() =>
    [
        new("W1", 3.1, 4.4, 5.2, 6.0, 7.3),
        new("W2", 3.6, 5.0, 5.9, 6.8, 8.1),
        new("W3", 2.8, 4.1, 4.9, 5.6, 6.4),
        new("W4", 4.0, 5.3, 6.1, 7.2, 8.9),
        new("W5", 3.3, 4.8, 5.6, 6.3, 7.0),
        new("W6", 2.6, 3.9, 4.7, 5.5, 6.2),
    ];
}
