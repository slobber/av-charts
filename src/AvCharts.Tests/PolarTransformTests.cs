using Avalonia;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the coordinate system a pie is drawn in: that an angle and a radius reach the right place, that the
/// circle is the largest that fits the plot, and that a point can be read back as the angle and radius that
/// put it there.
/// </summary>
public class PolarTransformTests
{
    [Fact]
    public void An_angle_and_a_radius_reach_a_point_on_the_circle()
    {
        var transform = Circle(new Rect(0, 0, 200, 100));

        // Nought turns is three o'clock, which from a middle of fifty, a hundred across and fifty up is the
        // point on the rim: the radius is the smaller half of the plot, so ten rows off the top.
        Assert.True(transform.TryUnitToPoint(0, 1, out var threeOClock));

        Equal(new Point(150, 50), threeOClock);

        // A quarter turn is twelve o'clock, half a turn is nine, three quarters is six.
        Assert.True(transform.TryUnitToPoint(0.25, 1, out var twelve));
        Assert.True(transform.TryUnitToPoint(0.5, 1, out var nine));
        Assert.True(transform.TryUnitToPoint(0.75, 1, out var six));

        Equal(new Point(100, 0), twelve);
        Equal(new Point(50, 50), nine);
        Equal(new Point(100, 100), six);
    }

    [Fact]
    public void The_circle_is_the_largest_that_fits_the_plot_and_sits_in_the_middle_of_it()
    {
        // A plot twice as wide as it is tall holds a circle whose radius is half its height - not an ellipse
        // touching all four sides, which is what mapping the two halves separately would draw.
        var transform = Circle(new Rect(20, 40, 400, 200));

        Assert.Equal(new Point(220, 140), transform.Origin);
        Assert.Equal(100, transform.Radius);

        Assert.True(transform.TryUnitToPoint(0.5, 1, out var nine));

        Equal(new Point(120, 140), nine);
    }

    [Fact]
    public void A_radius_of_nought_is_the_middle_and_one_beyond_it_is_outside_the_circle()
    {
        var transform = Circle(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryUnitToPoint(0.3, 0, out var middle));
        Equal(new Point(50, 50), middle);

        // A host that explodes a wedge asks for more than the circle's own radius and is placed where it asked
        // to be: the transform maps, and what may be drawn outside the plot is the series' business.
        Assert.True(transform.TryUnitToPoint(0, 1.5, out var outside));
        Equal(new Point(125, 50), outside);
    }

    [Fact]
    public void An_angle_of_a_whole_turn_comes_back_to_where_it_started()
    {
        var transform = Circle(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryUnitToPoint(0, 1, out var start));
        Assert.True(transform.TryUnitToPoint(1, 1, out var round));
        Assert.True(transform.TryUnitToPoint(-1, 1, out var backwards));

        Equal(start, round);
        Equal(start, backwards);
    }

    [Fact]
    public void A_place_can_be_read_back_as_the_angle_and_radius_that_put_it_there()
    {
        var transform = Circle(new Rect(0, 0, 200, 100));

        // Twelve o'clock, half way out: a quarter turn and a half.
        Assert.True(transform.TryPointToValue(new Point(100, 25), out var angle, out var radius));

        Assert.Equal(0.25, angle.Number, 9);
        Assert.Equal(0.5, radius.Number, 9);

        // And nine o'clock is half a turn rather than minus half: an angle outside a whole turn is turned back
        // into one, which is what a hit test comparing angles relies on.
        Assert.True(transform.TryPointToValue(new Point(50, 50), out var half, out var rim));

        Assert.Equal(0.5, half.Number, 9);
        Assert.Equal(1.0, rim.Number, 9);

        // Below the middle is more than half a turn rather than less, so that every angle of the circle is read
        // as a number between nought and one.
        Assert.True(transform.TryPointToValue(new Point(100, 75), out var six, out _));

        Assert.Equal(0.75, six.Number, 9);
    }

    [Fact]
    public void A_place_off_the_plot_is_still_read()
    {
        // The transform is a coordinate system rather than a box: what falls outside the circle is the series'
        // business to refuse, and a radius of more than one is what says so.
        var transform = Circle(new Rect(0, 0, 100, 100));

        Assert.True(transform.TryPointToValue(new Point(150, 50), out var angle, out var radius));
        Assert.Equal(0, angle.Number, 9);
        Assert.Equal(2, radius.Number, 9);
    }

    [Fact]
    public void A_plot_with_no_room_has_no_circle()
    {
        var transform = Circle(new Rect(0, 0, 0, 0));

        Assert.False(transform.TryUnitToPoint(0, 1, out _));
        Assert.False(transform.TryPointToValue(new Point(0, 0), out _, out _));
    }

    [Fact]
    public void A_direction_that_is_not_a_number_has_no_place()
    {
        var transform = Circle(new Rect(0, 0, 100, 100));

        Assert.False(transform.TryUnitToPoint(double.NaN, 1, out _));
        Assert.False(transform.TryUnitToPoint(0, double.NaN, out _));
    }

    [Fact]
    public void A_turn_is_as_long_as_the_circle_it_is_drawn_on()
    {
        // What an axis asks for when it works out how many labels fit: a turn round the rim is the circle's
        // circumference, and a radius is the circle's own radius.
        var transform = new Probe(new Rect(0, 0, 100, 300));

        Assert.Equal(2 * Math.PI * 50, transform.LengthOf(ChartDimension.Angle));
        Assert.Equal(50, transform.LengthOf(ChartDimension.Radius));
    }

    [Fact]
    public void An_axis_of_its_own_reads_the_values_through_it()
    {
        // A chart with a scale of its own - a gauge - puts an angle axis on the transform, and then a value on
        // that axis is what says how far round the point is: sixty degrees is a sixth of a turn.
        var transform = Circle(new Rect(0, 0, 100, 100));

        transform.SetAxis(ChartDimension.Angle, new LinearAxis { Minimum = 0, Maximum = 360 });

        Assert.True(transform.TryValueToPoint(60, 1, out var point));
        Equal(new Point(50 + (50 * Math.Cos(Math.PI / 3)), 50 - (50 * Math.Sin(Math.PI / 3))), point);

        Assert.True(transform.TryPointToValue(point, out var angle, out _));
        Assert.Equal(60, angle.Number, 6);
    }

    /// <summary>A transform that will say how long a dimension is, which is a protected answer.</summary>
    private sealed class Probe : PolarTransform
    {
        /// <summary>Makes one over a plot.</summary>
        /// <param name="plot">The rectangle the circle is drawn in.</param>
        public Probe(Rect plot) => PlotBounds = plot;

        /// <summary>How long a dimension is, in pixels.</summary>
        /// <param name="dimension">The dimension.</param>
        /// <returns>The length.</returns>
        public double LengthOf(ChartDimension dimension) => GetDimensionLength(dimension);
    }

    /// <summary>A transform with a plot and no axes, which is what a pie hands it.</summary>
    /// <param name="plot">The rectangle the circle is drawn in.</param>
    /// <returns>The transform.</returns>
    private static PolarTransform Circle(Rect plot) => new() { PlotBounds = plot };

    /// <summary>Asserts that a place is where the arithmetic says, to a thousandth of a pixel.</summary>
    /// <param name="expected">What the arithmetic says.</param>
    /// <param name="actual">Where the transform put it.</param>
    private static void Equal(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }
}
