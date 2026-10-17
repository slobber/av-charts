using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Drawing;
using AvCharts.Elements;
using AvCharts.Series;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the things on a chart that are not series: that an annotation is painted under the data, that one which
/// asks for room gets it, that changing one shows up, and that the layers are painted in the order the chart says
/// rather than in the order they happened to be created in.
/// </summary>
/// <remarks>
/// This is the fourth extension contract - a threshold line, a reference band, a marker for an event - and the
/// paint order it depends on, which the goldens found after the first version had the grid over the series.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ElementTests
{
    [Fact]
    public void A_control_an_element_carries_stands_on_the_reading_it_names()
    {
        var note = new Note(2, 3);
        var chart = Chart(Line([1.0, 2.0, 3.0]), note);
        var carried = chart.GetVisualChildren().OfType<Border>().Single();
        var at = Position(chart, 2, 3);

        // The chart carries the control the element handed over, and it stands where the element said: centred on
        // the point its values turn into.
        // Within a pixel: the control lands on the pixel grid, and what is being asked is where it stands rather
        // than which pixel its edge rounded to.
        Assert.InRange(carried.Bounds.Center.X, at.X - 1, at.X + 1);
        Assert.InRange(carried.Bounds.Center.Y, at.Y - 1, at.Y + 1);
        Assert.True(carried.IsVisible, "a control with somewhere to stand should be shown");
    }

    [Fact]
    public void The_carried_control_follows_the_window()
    {
        var note = new Note(2, 3);
        var chart = Chart(Line([1.0, 2.0, 3.0]), note);
        var carried = chart.GetVisualChildren().OfType<Border>().Single();
        var before = carried.Bounds;

        // Dragging the readings right shows earlier ones, so the reading the control stands on moves right with
        // them - which is the whole of what "it stands on a reading" means.
        chart.PanBy(40, 0);
        Lay(chart);

        Assert.True(
            carried.Bounds.X > before.X + 20,
            $"the control stayed at {carried.Bounds.X}, having been at {before.X}");
    }

    [Fact]
    public void A_control_with_nowhere_to_stand_is_hidden_and_a_taken_away_element_takes_its_control()
    {
        var note = new Note(2, 3);
        var chart = Chart(Line([1.0, 2.0, 3.0]), note);
        var carried = chart.GetVisualChildren().OfType<Border>().Single();

        // Nowhere to stand - the element says so - is hidden rather than drawn in a corner: what the element means
        // is "not here", not "over there".
        note.Nowhere = true;
        Lay(chart);

        Assert.False(carried.IsVisible);

        // And off the chart, the control goes with it: a chart that kept it would go on drawing a note for an
        // element nobody has any more.
        chart.Elements.Remove(note);

        Assert.DoesNotContain(carried, chart.GetVisualChildren());
    }

    [Fact]
    public void The_carried_control_is_read_over_the_layers()
    {
        var note = new Note(2, 3);
        var chart = Chart(Line([1.0, 2.0, 3.0]), note);
        var children = chart.GetVisualChildren().ToArray();
        var carried = Array.IndexOf(children, children.OfType<Border>().Single());

        // Over every layer: a note a host put on a reading is not something a grid line is drawn across.
        Assert.True(
            carried > Array.IndexOf(children, chart.SeriesLayer),
            "the control should be read over the series");
        Assert.True(
            carried > Array.IndexOf(children, chart.AnnotationLayer),
            "the control should be read over the annotations");
    }

    [Fact]
    public void The_layers_are_painted_in_the_order_the_chart_says()
    {
        var chart = Chart(Line([1.0, 2.0, 3.0]));

        // Furniture, then the data, then the readings - whatever order the layers were made in. The chart type
        // makes its axes after the layers the base class makes, so this is not the order they arrive in.
        Assert.Equal(
            ["AnnotationLayer", "AxisLayer", "AxisLayer", "SeriesLayer", "SelectionLayer", "CrosshairLayer"],
            chart.GetVisualChildren().OfType<ChartLayer>().Select(layer => layer.GetType().Name).ToArray());

        // And the tips are above all of it, because a tip is something to read rather than something to paint
        // with.
        Assert.IsType<Canvas>(chart.GetVisualChildren().Last());
    }

    [Fact]
    public void An_annotation_is_painted_under_the_data()
    {
        var band = new Band(2, Colors.SeaGreen);
        var chart = Chart(Line([1.0, 2.0, 3.0]), band);
        var bitmap = Picture(chart);
        var rows = Ink.Bounds(bitmap, Colors.SeaGreen, new Rect(0, 0, Width, Height));

        Assert.True(rows.Bottom >= rows.Top, "the band should have been painted");

        // The series crosses the band, and where it does it is the series that shows: an annotation is context
        // for the data rather than a lid on it.
        var across = new Rect(0, rows.Top, Width, Math.Max(1, rows.Bottom - rows.Top + 1));

        Assert.True(Ink.Count(bitmap, Colors.Magenta, across) > 0, "the series should show through the band");
    }

    [Fact]
    public void An_annotation_that_asks_for_room_moves_the_axes()
    {
        // A band the data is expected to stay inside, which is the one kind of annotation that wants room made
        // for it: the axes grow to cover it even though no reading is anywhere near it.
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis(),
        };

        chart.Series.Add(Line([1.0, 2.0, 3.0]));
        Lay(chart);

        var without = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        chart.Elements.Add(new Limits(-2, 8));
        Lay(chart);

        var with = chart.Transform.GetLayout(ChartDimension.Y).UnitRange;

        Assert.True(with.Minimum <= -2 && with.Maximum >= 8, $"the band asked for room and got {with}");
        Assert.True(without.Maximum < 8, "without the element the axis should not have reached that high");

        // And taking the element off gives the room back, which is what being registered with the transform
        // rather than merely drawn means.
        chart.Elements.Clear();
        Lay(chart);

        Assert.True(chart.Transform.GetLayout(ChartDimension.Y).UnitRange.Maximum < 8, "the axis should have shrunk back");
    }

    [Fact]
    public void An_annotation_that_changes_is_painted_where_it_now_is()
    {
        var band = new Band(2, Colors.SeaGreen);
        var chart = Chart(Line([1.0, 2.0, 3.0]), band);

        var before = Ink.Bounds(Picture(chart), Colors.SeaGreen, new Rect(0, 0, Width, Height)).Top;

        band.Move(3.5);

        var after = Ink.Bounds(Picture(chart), Colors.SeaGreen, new Rect(0, 0, Width, Height)).Top;

        // Higher up the chart, and higher up the picture: an element that says it changed is painted as it now
        // is, without the chart being told anything about it.
        Assert.True(after < before, $"the band should have moved up: {before} then {after}");
    }

    /// <summary>A horizontal bar of ink at a value: the smallest useful annotation.</summary>
    /// <summary>Where a pair of values is drawn on a chart whose axes both run from zero to four.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Position(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 4 * plot.Width), plot.Bottom - (y / 4 * plot.Height));
    }

    /// <summary>
    /// An element that carries a control of its own and stands it on a reading: the half of S4 that is a control
    /// rather than paint.
    /// </summary>
    private sealed class Note : ChartElement
    {
        private readonly Border _control = new()
        {
            Width = 40,
            Height = 12,
            Background = Brushes.HotPink,
        };

        public Note(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The value it stands on, across.</summary>
        public double X { get; }

        /// <summary>The value it stands on, up.</summary>
        public double Y { get; }

        /// <summary>Set by a test that wants an element with nowhere to put its control.</summary>
        public bool Nowhere { get; set; }

        /// <summary>Where the control is stood, worked out when the mapping moves.</summary>
        public Point? At { get; private set; }

        public override Layoutable? Visual => _control;

        public override Point? VisualAnchor => At;

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            // Nothing: everything this element is, the control it carries says.
        }

        protected override void MappingChanged() =>
            At = Nowhere || Transform is not { } transform || !transform.TryValueToPoint(X, Y, out var point)
                ? null
                : point;
    }

    private sealed class Band : ChartElement
    {
        private readonly Color _colour;
        private double _value;

        public Band(double value, Color colour)
        {
            _value = value;
            _colour = colour;
        }

        /// <summary>Puts the bar somewhere else, and says that it changed.</summary>
        /// <param name="value">The value to draw at.</param>
        public void Move(double value)
        {
            _value = value;
            Invalidate();
        }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(_value), ChartValue.FromNumber(_value), out var point))
            {
                return;
            }

            var plot = renderContext.PlotBounds;

            context.FillRectangle(
                new SolidColorBrush(_colour),
                new Rect(plot.Left, point.Y - 3, plot.Width, 6));
        }
    }

    /// <summary>A band the data is meant to stay between, which asks the axes for room.</summary>
    private sealed class Limits : ChartElement
    {
        private readonly double _low;
        private readonly double _high;

        public Limits(double low, double high)
        {
            _low = low;
            _high = high;
        }

        protected override void DescribeData(DataDescription description)
        {
            description.Add(ChartDimension.Y, _low);
            description.Add(ChartDimension.Y, _high);
        }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(_low), ChartValue.FromNumber(_low), out var low) ||
                !renderContext.TryPoint(ChartValue.FromNumber(_high), ChartValue.FromNumber(_high), out var high))
            {
                return;
            }

            var plot = renderContext.PlotBounds;

            context.FillRectangle(
                new SolidColorBrush(Colors.SeaGreen, 0.2),
                new Rect(plot.Left, high.Y, plot.Width, low.Y - high.Y));
        }
    }

    /// <summary>Draws the chart the way the golden tests do, so a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
        bitmap.Render(chart);
        return bitmap;
    }

    /// <summary>Lays the chart out, which is where the ranges are worked out.</summary>
    /// <param name="chart">The chart.</param>
    private static void Lay(CartesianChart chart)
    {
        chart.Measure(new Size(Width, Height));
        chart.Arrange(new Rect(0, 0, Width, Height));
    }

    /// <summary>A series of readings, one per value, drawn in magenta so a test can look for it.</summary>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(double[] values) =>
        new()
        {
            ItemsSource = values,
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Magenta,
            StrokeThickness = 3,
        };

    /// <summary>A chart with one series and one annotation on it, laid out.</summary>
    /// <param name="series">The series.</param>
    /// <param name="element">The annotation, when there is one.</param>
    /// <returns>The chart.</returns>
    private static ChartProbe Chart(SeriesBase series, ChartElement? element = null)
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
        };

        chart.Series.Add(series);

        if (element is not null)
        {
            chart.Elements.Add(element);
        }

        Lay(chart);
        return chart;
    }

    private const int Width = 400;
    private const int Height = 200;

    /// <summary>A chart whose elements a test can add to.</summary>
    private sealed class ChartProbe : CartesianChart;
}
