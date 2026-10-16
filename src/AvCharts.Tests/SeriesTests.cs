using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Axes;
using AvCharts.Data;
using AvCharts.Series;
using AvCharts.Transforms;
using Xunit;

namespace AvCharts.Tests;

[Collection(AvaloniaCollection.Name)]
public class SeriesTests
{
    private static DataTransform Transform()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 100, 100) };
        transform.SetAxis(ChartDimension.X, new LinearAxis());
        transform.SetAxis(ChartDimension.Y, new LinearAxis());
        return transform;
    }

    private static SeriesRenderContext Context(DataTransform transform) =>
        new(transform, transform.PlotBounds, 1);

    [Fact]
    public void The_index_stands_in_for_a_missing_horizontal_selector()
    {
        var transform = Transform();
        var series = new Probe { ItemsSource = [1.0, 2.0, 3.0], YSelector = value => value };
        transform.AddElement(series);

        Assert.Equal([0d, 1d, 2d], series.XExposed);
        Assert.Equal([1d, 2d, 3d], series.YExposed);
    }

    [Fact]
    public void A_record_without_a_vertical_value_keeps_its_place()
    {
        // Dropping the record would shift every later point one slot to the left and draw a line that is not
        // the data. The record stays; only its ink is missing.
        var transform = Transform();
        var series = new Probe { ItemsSource = [1.0, 2.0, 3.0] };
        transform.AddElement(series);

        Assert.Equal(3, series.YExposed.Length);
        Assert.True(double.IsNaN(series.YExposed[1]), "the record is there, with nothing in it");
    }

    [Fact]
    public void Filter_drops_records_before_they_are_read()
    {
        var transform = Transform();
        var series = new Probe
        {
            ItemsSource = [1.0, 2.0, 3.0],
            YSelector = value => value,
            Filter = value => value > 1,
        };
        transform.AddElement(series);

        // Two records are left, and they close up behind the dropped one: a dropped record takes up no slot at
        // all, which is what makes it a different thing from a missing value.
        Assert.Equal([0d, 1d], series.XExposed);
        Assert.Equal([2d, 3d], series.YExposed);
    }

    [Fact]
    public void A_missing_value_breaks_the_run_rather_than_moving_to_the_origin()
    {
        var transform = Transform();
        var series = new Probe { ItemsSource = [1.0, double.NaN, 3.0], YSelector = value => value };
        transform.AddElement(series);

        var runs = series.Runs(Context(transform));

        Assert.Equal(2, runs.Count);
        Assert.Single(runs[0]);
        Assert.Single(runs[1]);
        Assert.Equal(new Point(0, 100), runs[0][0]);
        Assert.Equal(new Point(100, 0), runs[1][0]);
    }

    [Fact]
    public void A_value_the_axis_cannot_place_breaks_the_run_too()
    {
        var transform = Transform();
        var series = new Probe
        {
            ItemsSource = [1.0, 2.0, 3.0],
            YSelector = value => value == 2 ? new DateTime(2026, 10, 13) : (ChartValue)value,
        };
        transform.AddElement(series);

        var runs = series.Runs(Context(transform));

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void An_absent_source_is_not_an_error()
    {
        var transform = Transform();
        var series = new Probe();
        transform.AddElement(series);

        Assert.Empty(series.Runs(Context(transform)));
    }

    [Fact]
    public void Sampling_thins_a_run_that_is_denser_than_the_plot()
    {
        var transform = Transform();
        var series = new Probe
        {
            ItemsSource = Enumerable.Range(0, 1_000).Select(index => index == 500 ? 900d : 10d).ToArray(),
            YSelector = value => value,
            Sampling = SamplingMode.MinMax,
        };
        transform.AddElement(series);

        // A hundred pixel columns for a thousand points.
        var runs = series.Runs(new SeriesRenderContext(transform, new Rect(0, 0, 100, 100), 1));

        Assert.Single(runs);
        Assert.True(runs[0].Count < 300, $"kept {runs[0].Count} points for a hundred columns");
        Assert.Contains(runs[0], point => point.Y < 50);
    }

    [Fact]
    public void Sampling_leaves_a_run_that_already_fits_alone()
    {
        var transform = Transform();
        var series = new Probe
        {
            ItemsSource = [1.0, 5.0, 3.0, 8.0, 2.0],
            YSelector = value => value,
            Sampling = SamplingMode.Lttb,
        };
        transform.AddElement(series);

        var runs = series.Runs(new SeriesRenderContext(transform, new Rect(0, 0, 100, 100), 1));

        Assert.Single(runs);
        Assert.Equal(5, runs[0].Count);
    }

    [Fact]
    public void Sampling_never_joins_the_two_sides_of_a_gap()
    {
        var transform = Transform();
        var dense = Enumerable.Range(0, 400).Select(index => 10d).ToArray();
        var series = new Probe
        {
            ItemsSource = [.. dense, double.NaN, .. dense],
            YSelector = value => value,
            Sampling = SamplingMode.MinMax,
        };
        transform.AddElement(series);

        var runs = series.Runs(new SeriesRenderContext(transform, new Rect(0, 0, 100, 100), 1));

        // Two runs, because a gap is still a gap however dense the data either side of it is.
        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void Changing_the_sampling_mode_asks_for_a_repaint()
    {
        var series = new Probe();
        var repaints = 0;
        series.Invalidated += (_, _) => repaints++;

        series.Sampling = SamplingMode.Average;
        Assert.Equal(1, repaints);

        // Setting the same mode again is not a change.
        series.Sampling = SamplingMode.Average;
        Assert.Equal(1, repaints);
    }

    [Fact]
    public void The_phases_run_in_the_documented_order()
    {
        var transform = Transform();
        var series = new Probe { ItemsSource = [1.0, 2.0], YSelector = value => value };
        transform.AddElement(series);

        using var bitmap = new RenderTargetBitmap(new PixelSize(100, 100));
        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(context, Context(transform));
        }

        Assert.Equal(["UpdateData", "DescribeData", "BuildGeometry", "Draw"], series.Calls);
    }

    [Fact]
    public void DescribeData_sizes_both_axes_from_the_values()
    {
        var transform = Transform();
        var series = new Probe { ItemsSource = [1.0, 2.0, 3.0], YSelector = value => value };
        transform.AddElement(series);

        transform.Update();

        Assert.Equal(new AxisUnitRange(0, 2), transform.GetLayout(ChartDimension.X).UnitRange);
        Assert.Equal(new AxisUnitRange(1, 3), transform.GetLayout(ChartDimension.Y).UnitRange);
    }

    [Fact]
    public void A_change_asks_for_a_repaint()
    {
        var series = new Probe { ItemsSource = [1.0] };
        var repaints = 0;
        series.Invalidated += (_, _) => repaints++;

        series.ItemsSource = [1.0, 2.0];
        Assert.Equal(1, repaints);

        series.Filter = value => value > 0;
        Assert.Equal(2, repaints);

        // Assigning the same source again is not a change, and does not cause a second repaint.
        var source = new List<double> { 1.0 };
        series.ItemsSource = source;
        series.ItemsSource = source;
        Assert.Equal(3, repaints);
    }

    [Fact]
    public void Painting_a_line_puts_ink_on_the_surface()
    {
        var transform = Transform();
        var series = new LineSeries<double>
        {
            ItemsSource = [0.0, 5.0, 10.0],
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };
        transform.AddElement(series);

        var bitmap = new RenderTargetBitmap(new PixelSize(100, 100));
        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(context, Context(transform));
        }

        Assert.True(Ink.Count(bitmap) > 0, "a line with a stroke should have painted something");
    }

    private sealed class Probe : CartesianSeries<double>
    {
        public List<string> Calls { get; } = [];

        public double[] XExposed => XValues.ToArray();

        public double[] YExposed => YValues.ToArray();

        public IReadOnlyList<IReadOnlyList<Point>> Runs(in SeriesRenderContext context) => BuildRuns(in context);

        protected override void UpdateData()
        {
            Calls.Add("UpdateData");
            base.UpdateData();
        }

        protected override void DescribeData(DataDescription description)
        {
            Calls.Add("DescribeData");
            base.DescribeData(description);
        }

        protected override void BuildGeometry(in SeriesRenderContext context) => Calls.Add("BuildGeometry");

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext) => Calls.Add("Draw");
    }
}
