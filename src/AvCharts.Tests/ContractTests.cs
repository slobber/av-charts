using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the extension contracts the design brief freezes: a custom series is driven through its four hooks in
/// order and can draw from what it is handed, a custom marker shape is an interface rather than a base class, a
/// custom axis brings its own ladder, and a coordinate system of one's own is a transform a chart can be given.
/// </summary>
/// <remarks>
/// A subclass in a test project is the only honest proof that an extension point is one: everything here is
/// written the way a host would write it, against the public API only.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ContractTests
{
    [Fact]
    public void A_series_is_driven_through_its_hooks_in_the_documented_order()
    {
        var series = new TracedSeries { ItemsSource = [1.0, 2.0, 3.0], XSelector = value => value, YSelector = value => value };
        var chart = Chart(series);
        _ = Picture(chart);

        // Laying the chart out is where the data is read and described to the axes; painting is where the
        // geometry is built and drawn. The order of firsts is the contract.
        Assert.Equal(["update", "describe", "build", "draw"], series.Steps
            .Where(step => step is "update" or "describe" or "build" or "draw")
            .Distinct()
            .ToArray());

        // And the geometry the series built is what it drew, once, from the context it was handed.
        Assert.Equal(3, series.Marks);
    }

    [Fact]
    public void A_custom_series_draws_from_the_context_it_is_handed()
    {
        var series = new TracedSeries { ItemsSource = [1.0, 2.0, 3.0], XSelector = value => value, YSelector = value => value };
        var chart = Chart(series);

        // Everything a series needs to place ink came out of the context: the plot rectangle and the mapping.
        // The assertion is that the marks are where the chart says those values are.
        foreach (var item in series.Items)
        {
            Assert.NotNull(item.Position);
            Assert.True(new Rect(0, 0, Width, Height).Contains(item.Position!.Value));
        }

        // And the marks it built are on the picture, drawn in the ink it chose.
        Assert.True(Painted(chart) > 0, "the series should have painted its marks");
    }

    [Fact]
    public void A_series_with_nothing_to_place_draws_nothing()
    {
        // The degenerate case: every record missing. The hooks still run - the series was asked - and no ink is
        // made, which is the difference between "no readings" and "readings at zero".
        var series = new TracedSeries
        {
            ItemsSource = [1.0, 2.0],
            XSelector = value => value,
            YSelector = _ => ChartValue.Empty,
        };

        var chart = Chart(series);
        _ = Picture(chart);

        Assert.Equal(0, series.Marks);
        Assert.Equal(["update", "describe", "build", "draw"], series.Steps
            .Where(step => step is "update" or "describe" or "build" or "draw")
            .Distinct()
            .ToArray());
    }

    [Fact]
    public void A_custom_marker_draws_its_own_shape()
    {
        var marker = new TriangleMarker();
        var series = new LineSeries<double>
        {
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => value,
            YSelector = value => value,
            Marker = marker,
            MarkerSize = 4,
            MarkerFill = Brushes.Magenta,
        };

        var chart = Chart(series);
        var bitmap = Picture(chart);

        // One mark per record, drawn through the interface rather than a base class, and filled with the series'
        // own ink.
        Assert.Equal(3, marker.Drawn);
        Assert.True(Ink.Count(bitmap, Colors.Magenta) > 0, "the marks should have been painted");
    }

    [Fact]
    public void A_custom_axis_supplies_its_own_ladder()
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new ThreesAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        chart.Series.Add(Line([1.0, 2.0, 3.0]));
        Lay(chart);

        // The axis said every interval is three, and every interval is three - the chart does not round it off
        // to something it prefers.
        var layout = chart.Transform.GetLayout(ChartDimension.X);

        Assert.Equal(3, layout.MajorStep.Length, 6);
        Assert.Equal(3, layout.MajorStep.Floor(4), 6);
        Assert.Equal(6, layout.MajorStep.Advance(3), 6);

        // And the marks the renderer draws are that ladder rather than the one it would have picked itself: a
        // range of nought to four would bring a tick at two, and this axis has no tick at two.
        var labels = chart.HorizontalRenderer
            .GetTicks(layout, chart.PlotBounds)
            .Select(tick => tick.Label)
            .ToArray();

        Assert.Contains("3", labels);
        Assert.DoesNotContain("2", labels);
    }

    [Fact]
    public void A_chart_can_be_given_a_transform_of_its_own()
    {
        var plain = Chart(Line([1.0, 2.0, 3.0]));
        var mirrored = Chart(Line([1.0, 2.0, 3.0]));
        mirrored.Transform = new MirroredTransform { };
        Lay(mirrored);

        // Everything the chart maps goes through the transform it was given: the values to points, the record
        // positions a series keeps, and the way back for a hit test.
        var here = plain.DataToPoint(ChartValue.FromNumber(1), ChartValue.FromNumber(2))!.Value;
        var there = mirrored.DataToPoint(ChartValue.FromNumber(1), ChartValue.FromNumber(2))!.Value;

        Assert.NotEqual(here.X, there.X, 3);
        Assert.Equal(here.Y, there.Y, 6);
        Assert.Equal(
            2 * mirrored.PlotBounds.Center.X - here.X,
            there.X,
            6);

        Assert.Equal(
            plain.Series[0].Items.Single(item => item.Index == 1).Position!.Value.X,
            2 * mirrored.PlotBounds.Center.X - mirrored.Series[0].Items.Single(item => item.Index == 1).Position!.Value.X,
            6);

        // And the way back is the chart's own mapping rather than the one it started with.
        Assert.Equal(1, mirrored.PointToData(there)!.Value.X.Number, 6);
        Assert.Equal(2, mirrored.PointToData(there)!.Value.Y.Number, 6);
    }

    [Fact]
    public void What_the_transform_holds_is_what_the_chart_shows()
    {
        var series = Line([1.0, 2.0, 3.0]);
        var threshold = new Threshold(2);
        var chart = Chart(series);

        Assert.Equal([series], chart.Transform.Elements);

        // An element joins the transform when it joins the chart, and leaves with it - which is what makes the
        // axes cover what is drawn.
        chart.Elements.Add(threshold);

        Assert.Equal(2, chart.Transform.Elements.Count);
        Assert.Contains(threshold, chart.Transform.Elements);

        chart.Series.Remove(series);

        Assert.Equal([threshold], chart.Transform.Elements);

        chart.Elements.Remove(threshold);

        Assert.Empty(chart.Transform.Elements);
    }

    [Fact]
    public void Clearing_a_list_leaves_nothing_registered()
    {
        var series = Line([1.0, 2.0, 3.0]);
        var chart = Chart(series);

        chart.Elements.Add(new Threshold(2));

        Assert.Equal(2, chart.Transform.Elements.Count);

        // Clearing a list says "all of them" and nothing about which ones, so the chart has to let go of what it
        // registered rather than work the difference out of the event - which is what it used to do, and what
        // left a cleared chart still ranging and painting the series it no longer had.
        chart.Series.Clear();
        chart.Elements.Clear();

        Assert.Empty(chart.Transform.Elements);
        Assert.Empty(chart.SeriesLayer.Series);
        Assert.Empty(chart.AnnotationLayer.Elements);
    }

    /// <summary>A series that writes down which hook it was in, and draws a mark at every record it can place.</summary>
    private sealed class TracedSeries : CartesianSeries<double>
    {
        private List<Point> _marks = [];

        /// <summary>The hooks, in the order they were called.</summary>
        public List<string> Steps { get; } = [];

        /// <summary>How many marks the last geometry had.</summary>
        public int Marks { get; private set; }

        protected override void UpdateData()
        {
            Steps.Add("update");
            base.UpdateData();
        }

        protected override void DescribeData(DataDescription description)
        {
            Steps.Add("describe");
            base.DescribeData(description);
        }

        protected override void BuildGeometry(in SeriesRenderContext context)
        {
            Steps.Add("build");
            _marks = [];

            // What the context hands over is all a series has to work with, and it is enough: the records that
            // can be placed, and where.
            foreach (var (_, _, _, point) in PlacedRecords(context))
            {
                _marks.Add(point);
            }
        }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            Steps.Add("draw");
            Marks = _marks.Count;

            foreach (var mark in _marks)
            {
                context.DrawEllipse(Brushes.DarkOrange, null, mark, 3, 3);
            }
        }
    }

    /// <summary>A marker shape of one's own: the interface takes the batch and adds whatever it likes to it.</summary>
    private sealed class TriangleMarker : IMarkerRenderer
    {
        /// <summary>How many marks have been added to a batch.</summary>
        public int Drawn { get; private set; }

        public void AddTo(GeometryBatch batch, Point centre, double radius)
        {
            Drawn++;

            batch.AddPolyline(
                [
                    new Point(centre.X, centre.Y - radius),
                    new Point(centre.X + radius, centre.Y + radius),
                    new Point(centre.X - radius, centre.Y + radius),
                ],
                isClosed: true);
        }
    }

    /// <summary>An axis whose ladder is what it says it is, rather than the one the base class would pick.</summary>
    private sealed class ThreesAxis : LinearAxis
    {
        protected override double NiceInterval(double target) => 3;
    }

    /// <summary>A transform that mirrors the horizontal dimension: a coordinate system of one's own.</summary>
    private sealed class MirroredTransform : DataTransform
    {
        public override bool TryValueToPoint(ChartValue x, ChartValue y, out Point point)
        {
            if (!base.TryValueToPoint(x, y, out var mirrored))
            {
                point = default;
                return false;
            }

            point = Mirror(mirrored);
            return true;
        }

        public override bool TryUnitToPoint(double x, double y, out Point point)
        {
            if (!base.TryUnitToPoint(x, y, out var mirrored))
            {
                point = default;
                return false;
            }

            point = Mirror(mirrored);
            return true;
        }

        public override bool TryPointToValue(Point point, out ChartValue x, out ChartValue y) =>
            base.TryPointToValue(Mirror(point), out x, out y);

        private Point Mirror(Point point) => new((2 * PlotBounds.Center.X) - point.X, point.Y);
    }

    /// <summary>A horizontal line at a value, which is the smallest useful thing that is not a series.</summary>
    private sealed class Threshold : ChartElement
    {
        private readonly Pen _pen = new(Brushes.SeaGreen, 2);

        public Threshold(double value) => Value = value;

        public double Value { get; }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (renderContext.TryUnitPoint(Value, Value, out var point))
            {
                context.DrawLine(_pen, new Point(renderContext.PlotBounds.Left, point.Y), new Point(renderContext.PlotBounds.Right, point.Y));
            }
        }
    }

    /// <summary>How much ink of the colour a test series draws in the picture carries.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The number of pixels.</returns>
    private static int Painted(CartesianChart chart) => Ink.Count(Picture(chart), Colors.DarkOrange);

    /// <summary>Draws the chart the way the golden tests do, so a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
        bitmap.Render(chart);
        return bitmap;
    }

    /// <summary>Lays the chart out, which is where the hooks run.</summary>
    /// <param name="chart">The chart.</param>
    private static void Lay(CartesianChart chart)
    {
        chart.Measure(new Size(Width, Height));
        chart.Arrange(new Rect(0, 0, Width, Height));
    }

    /// <summary>A series of readings, one per value.</summary>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(double[] values) =>
        new() { ItemsSource = values, XSelector = value => value, YSelector = value => value };

    /// <summary>A chart with one series on it, laid out.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The chart.</returns>
    private static ChartProbe Chart(Series series)
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
        };

        chart.Series.Add(series);
        Lay(chart);
        return chart;
    }

    private const int Width = 400;
    private const int Height = 200;

    /// <summary>A chart with the layers a test wants to reach.</summary>
    private sealed class ChartProbe : CartesianChart;
}
