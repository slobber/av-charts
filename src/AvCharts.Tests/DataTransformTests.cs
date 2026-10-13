using Avalonia;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class DataTransformTests
{
    /// <summary>An axis with an explicit range: what the data says never changes it.</summary>
    private static DataTransform Transform(Rect plot, double min = 0, double max = 10)
    {
        var transform = new DataTransform { PlotBounds = plot };
        transform.SetAxis(ChartDimension.X, new StubAxis(min, max));
        transform.SetAxis(ChartDimension.Y, new StubAxis(min, max));
        return transform;
    }

    [Fact]
    public void Values_map_onto_the_plot_rectangle()
    {
        var transform = Transform(new Rect(10, 10, 100, 50));

        Assert.True(transform.TryValueToPoint(0, 0, out var bottomLeft));
        Assert.Equal(new Point(10, 60), bottomLeft);

        Assert.True(transform.TryValueToPoint(10, 10, out var topRight));
        Assert.Equal(new Point(110, 10), topRight);
    }

    [Fact]
    public void The_vertical_dimension_is_flipped_because_screen_y_grows_downwards()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryValueToPoint(0, 0, out var low));
        Assert.True(transform.TryValueToPoint(0, 10, out var high));

        Assert.True(high.Y < low.Y);
    }

    [Fact]
    public void The_horizontal_dimension_is_not_flipped()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryValueToPoint(0, 0, out var low));
        Assert.True(transform.TryValueToPoint(10, 0, out var high));

        Assert.True(high.X > low.X);
    }

    [Fact]
    public void A_missing_value_has_no_position()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));

        Assert.False(transform.TryValueToPoint(ChartValue.Empty, 5, out _));
        Assert.False(transform.TryValueToPoint(5, ChartValue.Empty, out _));
    }

    [Fact]
    public void A_value_the_axis_does_not_understand_has_no_position()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));

        Assert.False(transform.TryValueToPoint(new DateTime(2026, 10, 13), 5, out _));
    }

    [Fact]
    public void A_dimension_without_an_axis_has_no_position()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 100, 100) };
        transform.SetAxis(ChartDimension.X, new StubAxis(0, 10));

        Assert.False(transform.TryValueToPoint(5, 5, out _));
    }

    [Fact]
    public void A_degenerate_range_maps_to_the_middle_instead_of_dividing_by_zero()
    {
        var transform = Transform(new Rect(0, 0, 100, 100), min: 5, max: 5);

        Assert.True(transform.TryValueToPoint(5, 5, out var point));
        Assert.Equal(new Point(50, 50), point);
    }

    [Fact]
    public void Points_map_back_to_values()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryPointToValue(new Point(25, 75), out var x, out var y));
        Assert.Equal(2.5, x.Number, 6);
        Assert.Equal(2.5, y.Number, 6);

        Assert.True(transform.TryPointToValue(new Point(0, 100), out var cornerX, out var cornerY));
        Assert.Equal(0, cornerX.Number, 6);
        Assert.Equal(0, cornerY.Number, 6);
    }

    [Fact]
    public void Values_taken_off_screen_still_map()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryValueToPoint(20, 20, out var point));
        Assert.Equal(200, point.X, 6);
        Assert.Equal(-100, point.Y, 6);
    }

    [Fact]
    public void Every_element_describes_its_data_and_the_axis_receives_the_union()
    {
        var transform = new DataTransform();
        var horizontal = new StubAxis();
        transform.SetAxis(ChartDimension.X, horizontal);

        transform.AddElement(new StubElement(ChartDimension.X, new BoundedValue(3), new BoundedValue(-2)));
        transform.AddElement(new StubElement(ChartDimension.X, new BoundedValue(7, LowerMargin: 1)));

        transform.Update();

        Assert.Equal(3, horizontal.BoundCount);
        Assert.Equal(-2, transform.GetLayout(ChartDimension.X).UnitRange.Minimum);
        Assert.Equal(7, transform.GetLayout(ChartDimension.X).UnitRange.Maximum);
    }

    [Fact]
    public void An_element_without_data_leaves_the_axis_empty()
    {
        var transform = new DataTransform();
        var horizontal = new StubAxis();
        transform.SetAxis(ChartDimension.X, horizontal);
        transform.AddElement(new StubElement());

        transform.Update();

        Assert.True(transform.GetLayout(ChartDimension.X).UnitRange.IsEmpty);
        Assert.Equal(0, horizontal.BoundCount);
    }

    [Fact]
    public void Moving_the_plot_remaps_without_describing_the_data_again()
    {
        var transform = Transform(new Rect(0, 0, 100, 100));
        var element = new StubElement(ChartDimension.X, new BoundedValue(10));
        transform.AddElement(element);

        transform.Update();
        var describesBefore = element.DescribeCount;
        var movesBefore = element.MappingChangedCount;

        transform.PlotBounds = new Rect(50, 50, 100, 100);
        transform.Update();

        Assert.Equal(describesBefore, element.DescribeCount);
        Assert.Equal(movesBefore + 1, element.MappingChangedCount);
        Assert.True(transform.TryValueToPoint(10, 5, out var point));
        Assert.Equal(150, point.X, 6);
    }

    [Fact]
    public void Updating_twice_does_no_work_and_notifies_no_one()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 100, 100) };
        var axis = new StubAxis(0, 10);
        transform.SetAxis(ChartDimension.X, axis);
        var element = new StubElement(ChartDimension.X, new BoundedValue(10));
        transform.AddElement(element);

        transform.Update();
        var movesAfterFirst = element.MappingChangedCount;

        transform.Update();
        transform.Update();

        Assert.Equal(movesAfterFirst, element.MappingChangedCount);
    }

    [Fact]
    public void Adding_an_element_invalidates_the_range_and_tells_the_axis()
    {
        var transform = new DataTransform();
        var axis = new StubAxis();
        transform.SetAxis(ChartDimension.X, axis);

        transform.Update();
        Assert.Equal(0, axis.BoundCount);

        transform.AddElement(new StubElement(ChartDimension.X, new BoundedValue(4)));
        transform.Update();

        Assert.Equal(1, axis.BoundCount);
        Assert.Equal(4, transform.GetLayout(ChartDimension.X).UnitRange.Maximum);
    }

    [Fact]
    public void An_element_that_registers_another_element_while_reacting_does_not_break_the_pass()
    {
        var transform = new DataTransform { PlotBounds = new Rect(0, 0, 100, 100) };
        transform.SetAxis(ChartDimension.X, new StubAxis(0, 10));
        var late = new StubElement(ChartDimension.X, new BoundedValue(9));
        var early = new StubElement(ChartDimension.X, new BoundedValue(1)) { OnMappingChanged = () => transform.AddElement(late) };
        transform.AddElement(early);

        // The notification pass runs over a snapshot, so an element added while reacting is picked up by the
        // next pass rather than half-way through this one.
        transform.Update();

        Assert.Equal(1, early.MappingChangedCount);
        Assert.Equal(0, late.MappingChangedCount);

        transform.Update();

        Assert.Equal(1, late.MappingChangedCount);
    }

    private sealed class StubAxis : IAxis
    {
        private readonly double? _fixedMaximum;
        private readonly double? _fixedMinimum;

        public StubAxis()
        {
        }

        public StubAxis(double minimum, double maximum)
        {
            _fixedMinimum = minimum;
            _fixedMaximum = maximum;
        }

        public int BoundCount { get; private set; }

        public ChartValueKind ValueKind => ChartValueKind.Number;

        public string DisplayName => "stub";

        public bool TryToUnit(ChartValue value, out double unit)
        {
            if (value.IsEmpty || value.Kind != ChartValueKind.Number)
            {
                unit = 0;
                return false;
            }

            unit = value.Number;
            return true;
        }

        public ChartValue FromUnit(double unit) => unit;

        public string FormatValue(double unit) => unit.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public AxisLayout Resolve(IReadOnlyList<BoundedValue> bounds, double lengthInPixels)
        {
            BoundCount = bounds.Count;

            var range = _fixedMinimum.HasValue
                ? new AxisUnitRange(_fixedMinimum.Value, _fixedMaximum!.Value)
                : bounds.Count == 0
                    ? AxisUnitRange.Empty
                    : new AxisUnitRange(bounds.Min(bound => bound.Minimum), bounds.Max(bound => bound.Maximum));

            return new AxisLayout(range, new EvenStep(1), 0.2, [], []);
        }
    }

    private sealed class StubElement : IChartElement
    {
        private readonly ChartDimension _dimension;
        private readonly BoundedValue[] _values;

        public StubElement()
            : this(ChartDimension.X)
        {
        }

        public StubElement(ChartDimension dimension, params BoundedValue[] declared)
        {
            _dimension = dimension;
            _values = declared;
        }

        public int DescribeCount { get; private set; }

        public int MappingChangedCount { get; private set; }

        public Action? OnMappingChanged { get; set; }

        public void DescribeData(DataDescription description)
        {
            DescribeCount++;

            foreach (var value in _values)
            {
                description.Add(_dimension, value);
            }
        }

        public void MappingChanged()
        {
            MappingChangedCount++;
            OnMappingChanged?.Invoke();
        }
    }
}
