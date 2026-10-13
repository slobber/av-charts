using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
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
        var series = new Probe { ItemsSource = [1.0, 2.0, 3.0], YSelector = value => value };

        Assert.Equal([(0d, 1d), (1d, 2d), (2d, 3d)], series.ValuesExposed.Select(value => ((double)value.X.Number, (double)value.Y.Number)));
    }

    [Fact]
    public void A_record_without_a_vertical_value_keeps_its_place()
    {
        // Dropping the record would shift every later point one slot to the left and draw a line that is not
        // the data. The record stays; only its ink is missing.
        var series = new Probe { ItemsSource = [1.0, 2.0, 3.0] };

        Assert.Equal(3, series.ValuesExposed.Count);
        Assert.True(series.ValuesExposed[1].Y.IsEmpty);
    }

    [Fact]
    public void Filter_drops_records_before_they_are_read()
    {
        var series = new Probe
        {
            ItemsSource = [1.0, 2.0, 3.0],
            YSelector = value => value,
            Filter = value => value > 1,
        };

        Assert.Equal(2, series.ValuesExposed.Count);
        Assert.Equal(2d, series.ValuesExposed[0].Y.Number);
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

        public IReadOnlyList<(ChartValue X, ChartValue Y)> ValuesExposed => Values;

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
