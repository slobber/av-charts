using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the half of the library a markup file reaches: a series whose readings are any object, and selectors
/// written as paths rather than as lambdas.
/// </summary>
/// <remarks>
/// A page cannot write a lambda and cannot write a type argument, so both of those have a second form here - and
/// the two forms have to be the same chart rather than two that look alike, which is what the picture is asked:
/// the same readings drawn through a path and drawn through a lambda leave the same ink.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class MarkupSeriesTests
{
    [Fact]
    public void A_path_draws_what_a_lambda_draws()
    {
        var sales = Sales();

        var byLambda = new LineSeries<Sale>
        {
            ItemsSource = sales,
            XSelector = sale => sale.Name,
            YSelector = sale => sale.Value,
            Stroke = Drawn,
        };

        // The same reading of the same records, written the way a page has to write it.
        var byPath = new LineSeries
        {
            ItemsSource = sales,
            XPath = "Name",
            YPath = "Value",
            Stroke = Drawn,
        };

        var (drawn, read) = Painted(byLambda, byPath);

        Assert.Equal(0, Ink.Differences(drawn, read));
    }

    [Fact]
    public void A_path_walks_into_a_step_inside_a_step_and_into_a_dictionary()
    {
        var sales = new[]
        {
            new Sale("North", new Region(3), 1),
            new Sale("South", new Region(5), 2),
        };

        var deep = new LineSeries
        {
            ItemsSource = sales,
            XPath = "Name",
            YPath = "Region.Value",
            Stroke = Drawn,
        };

        var shallow = new LineSeries<Sale>
        {
            ItemsSource = sales,
            XSelector = sale => sale.Name,
            YSelector = sale => sale.Region!.Value,
            Stroke = Drawn,
        };

        var (walked, written) = Painted(deep, shallow);

        Assert.Equal(0, Ink.Differences(walked, written));

        // And a record that is a dictionary rather than an object reads the same way, which is what a chart over
        // a DataTable or a row of a report is drawn from.
        var rows = new[]
        {
            new Dictionary<string, object> { ["Name"] = "North", ["Value"] = 3.0 },
            new Dictionary<string, object> { ["Name"] = "South", ["Value"] = 5.0 },
        };

        var byName = new LineSeries
        {
            ItemsSource = rows,
            XPath = "Name",
            YPath = "Value",
            Stroke = Drawn,
        };

        var byHand = new LineSeries<Sale>
        {
            ItemsSource = sales,
            XSelector = sale => sale.Name,
            YSelector = sale => sale.Region!.Value,
            Stroke = Drawn,
        };
        var (rows_, fields) = Painted(byName, byHand);

        Assert.Equal(0, Ink.Differences(rows_, fields));
    }

    [Fact]
    public void A_path_nobody_has_is_a_missing_value_rather_than_a_fault()
    {
        var series = new LineSeries
        {
            ItemsSource = Sales(),
            XPath = "Name",
            YPath = "NothingOfTheKind",
        };
        var chart = Chart(series);

        // Nothing to draw, and nothing thrown: a reading a chart cannot find is the same as a reading that is
        // not there, which is the rule the rest of the library follows.
        Assert.Equal(0, Ink.Count(Painted(chart, series)));
    }

    [Fact]
    public void The_types_a_page_can_name_are_the_ones_written_in_code()
    {
        // Each is its generic self over `object`, so a page and a host are configuring the same series: what the
        // markup types add is a name without a type argument, and nothing else at all.
        Assert.IsAssignableFrom<LineSeries<object>>(new LineSeries());
        Assert.IsAssignableFrom<ColumnSeries<object>>(new ColumnSeries());
        Assert.IsAssignableFrom<BarSeries<object>>(new BarSeries());
        Assert.IsAssignableFrom<AreaSeries<object>>(new AreaSeries());
        Assert.IsAssignableFrom<StepLineSeries<object>>(new StepLineSeries());
        Assert.IsAssignableFrom<StepAreaSeries<object>>(new StepAreaSeries());
        Assert.IsAssignableFrom<PlotSeries<object>>(new PlotSeries());
        Assert.IsAssignableFrom<BubbleSeries<object>>(new BubbleSeries());
        Assert.IsAssignableFrom<CandlestickSeries<object>>(new CandlestickSeries());
        Assert.IsAssignableFrom<HighLowCloseSeries<object>>(new HighLowCloseSeries());
        Assert.IsAssignableFrom<RangeSeries<object>>(new RangeSeries());
        Assert.IsAssignableFrom<ErrorSeries<object>>(new ErrorSeries());
        Assert.IsAssignableFrom<BoxSeries<object>>(new BoxSeries());
        Assert.IsAssignableFrom<PieSeries<object>>(new PieSeries());

        // And the paths a page sets reach the selectors the code would have set.
        var bubble = new BubbleSeries { RadiusPath = "Size" };
        var candles = new CandlestickSeries { ClosePath = "Close", OpenPath = "Open" };
        var bars = new HighLowCloseSeries { ClosePath = "Close", HighPath = "High", LowPath = "Low" };
        var ranges = new RangeSeries { LowPath = "Low", HighPath = "High" };
        var errors = new ErrorSeries { YPath = "Value", ErrorPath = "Error" };
        var boxes = new BoxSeries { MinimumPath = "Low", MedianPath = "Middle", MaximumPath = "High" };
        var pie = new PieSeries { ValuePath = "Value", NamePath = "Name", ExplodePath = "Out" };

        Assert.NotNull(bubble.RadiusSelector);
        Assert.NotNull(candles.CloseSelector);
        Assert.NotNull(bars.HighSelector);
        Assert.NotNull(bars.LowSelector);
        Assert.NotNull(ranges.LowSelector);
        Assert.NotNull(ranges.HighSelector);
        Assert.NotNull(errors.ErrorSelector);
        Assert.NotNull(boxes.MinimumSelector);
        Assert.NotNull(pie.ValueSelector);

        // The close is what a record is worth, so setting it by path sets the value axis' reading too.
        Assert.NotNull(candles.YSelector);
        Assert.NotNull(bars.YSelector);
    }

    /// <summary>Two series of readings over the ledger: what a chart of the sales is drawn from.</summary>
    /// <param name="Name">What the column is called.</param>
    /// <param name="Value">What it is worth.</param>
    private sealed record Sale(string Name, double Value)
    {
        public Sale(string name, Region region, double value)
            : this(name, value) => Region = region;

        public Region? Region { get; }
    }

    /// <summary>The part of a sale that is worth its own step in a path.</summary>
    /// <param name="Value">What it is worth.</param>
    private sealed record Region(double Value);

    /// <summary>The brush both forms are drawn with, so that what is compared is the shape and not the colour.</summary>
    private static readonly IBrush Drawn = Brushes.Red;
    /// <summary>Four readings, enough for a line and for the two forms of it to be compared.</summary>
    /// <returns>The readings.</returns>
    private static Sale[] Sales() =>
    [
        new("North", 3),
        new("South", 5),
        new("East", 2),
        new("West", 6),
    ];

    /// <summary>A chart of one series, measured, arranged and dressed.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(Series series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 8 },
        };

        chart.Series.Add(series);
        _ = new Window { Resources = ChartTheme.Create(), Content = chart };
        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));
        return chart;
    }

    /// <summary>Paints two series of the same readings, one picture each, and hands both over.</summary>
    /// <param name="first">One series.</param>
    /// <param name="second">The other.</param>
    /// <returns>The two pictures, in the same order.</returns>
    private static (RenderTargetBitmap First, RenderTargetBitmap Second) Painted(Series first, Series second)
    {
        var chart = Chart(first);

        chart.Series.Add(second);
        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));

        return (Painted(chart, first), Painted(chart, second));
    }

    /// <summary>Paints one series on its own, so that what it drew can be counted.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="series">The series.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Painted(CartesianChart chart, Series series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 300));

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }
}
