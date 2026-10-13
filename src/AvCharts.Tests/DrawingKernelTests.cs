using Avalonia;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

[Collection(AvaloniaCollection.Name)]
public class DrawingKernelTests
{
    [Fact]
    public void A_closed_run_of_points_becomes_one_geometry_covering_them()
    {
        var batch = new GeometryBatch();
        batch.AddPolyline([new Point(0, 0), new Point(10, 0), new Point(10, 5)], isClosed: true);

        var bounds = batch.Build().Bounds;

        Assert.Equal(0, bounds.X, 6);
        Assert.Equal(0, bounds.Y, 6);
        Assert.Equal(10, bounds.Width, 6);
        Assert.Equal(5, bounds.Height, 6);
    }

    [Fact]
    public void An_open_run_of_points_still_covers_them()
    {
        var batch = new GeometryBatch();
        batch.AddPolyline([new Point(2, 2), new Point(8, 6)]);

        var bounds = batch.Build().Bounds;

        Assert.Equal(6, bounds.Width, 6);
        Assert.Equal(4, bounds.Height, 6);
    }

    [Fact]
    public void A_run_of_fewer_than_two_points_adds_nothing_and_does_not_throw()
    {
        var batch = new GeometryBatch();
        batch.AddPolyline([new Point(5, 5)]);

        var bounds = batch.Build().Bounds;

        Assert.Equal(0, bounds.Width, 6);
        Assert.Equal(0, bounds.Height, 6);
    }

    [Fact]
    public void A_rectangle_becomes_its_own_geometry()
    {
        var batch = new GeometryBatch();
        batch.AddRectangle(new Rect(2, 3, 10, 4));

        var bounds = batch.Build().Bounds;

        Assert.Equal(2, bounds.X, 6);
        Assert.Equal(3, bounds.Y, 6);
        Assert.Equal(10, bounds.Width, 6);
        Assert.Equal(4, bounds.Height, 6);
    }

    [Fact]
    public void A_circle_is_two_half_arcs_that_reach_the_radius()
    {
        var batch = new GeometryBatch();
        batch.AddCircle(new Point(5, 5), 3);

        var bounds = batch.Build().Bounds;

        Assert.Equal(2, bounds.X, 3);
        Assert.Equal(2, bounds.Y, 3);
        Assert.Equal(6, bounds.Width, 3);
        Assert.Equal(6, bounds.Height, 3);
    }

    [Fact]
    public void A_diamond_reaches_the_radius_in_all_four_directions()
    {
        var batch = new GeometryBatch();
        batch.AddDiamond(new Point(5, 5), 3);

        var bounds = batch.Build().Bounds;

        Assert.Equal(2, bounds.X, 6);
        Assert.Equal(2, bounds.Y, 6);
        Assert.Equal(6, bounds.Width, 6);
        Assert.Equal(6, bounds.Height, 6);
    }

    [Fact]
    public void Shapes_with_no_size_add_nothing()
    {
        var batch = new GeometryBatch();
        batch.AddCircle(new Point(5, 5), 0);
        batch.AddDiamond(new Point(5, 5), -1);

        var bounds = batch.Build().Bounds;

        Assert.Equal(0, bounds.Width, 6);
        Assert.Equal(0, bounds.Height, 6);
    }

    [Fact]
    public void Many_shapes_come_out_as_one_geometry()
    {
        // The point of the batch: a thousand markers is one fill, not a thousand draws.
        var batch = new GeometryBatch();

        for (var index = 0; index < 1_000; index++)
        {
            batch.AddCircle(new Point(index % 100, index / 100), 2);
        }

        var bounds = batch.Build().Bounds;

        Assert.Equal(-2, bounds.X, 6);
        Assert.Equal(-2, bounds.Y, 6);
        Assert.Equal(103, bounds.Width, 6);
    }

    [Fact]
    public void A_batch_is_one_shot()
    {
        var batch = new GeometryBatch();
        batch.AddRectangle(new Rect(0, 0, 1, 1));
        batch.Build();

        Assert.True(batch.IsBuilt);
        Assert.Throws<InvalidOperationException>(() => batch.Build());
        Assert.Throws<InvalidOperationException>(() => batch.AddRectangle(new Rect(0, 0, 1, 1)));
    }

    [Fact]
    public void Snapping_puts_a_position_on_the_device_pixel_grid()
    {
        var context = new SeriesRenderContext(new DataTransform(), new Rect(0, 0, 100, 100), 1);

        Assert.Equal(10, context.Snap(10.4), 6);
        Assert.Equal(11, context.Snap(10.6), 6);

        // At 200% one logical pixel is two device pixels, so the grid is twice as fine.
        var fine = new SeriesRenderContext(new DataTransform(), new Rect(0, 0, 100, 100), 2);

        Assert.Equal(10.5, fine.Snap(10.3), 6);
        Assert.Equal(10, fine.Snap(10.2), 6);
    }

    [Fact]
    public void An_odd_stroke_sits_half_a_pixel_off_the_grid_and_an_even_one_does_not()
    {
        var context = new SeriesRenderContext(new DataTransform(), new Rect(0, 0, 100, 100), 1);

        Assert.Equal(10.5, context.SnapStroke(10.2, thickness: 1), 6);
        Assert.Equal(10.5, context.SnapStroke(10.9, thickness: 1), 6);
        Assert.Equal(10, context.SnapStroke(10.2, thickness: 2), 6);

        // At 200% a one pixel stroke is two device pixels wide, so it belongs on the boundary again.
        var fine = new SeriesRenderContext(new DataTransform(), new Rect(0, 0, 100, 100), 2);

        Assert.Equal(10, fine.SnapStroke(10.2, thickness: 1), 6);
    }

    [Fact]
    public void A_context_without_a_usable_scaling_falls_back_to_one()
    {
        var context = new SeriesRenderContext(new DataTransform(), new Rect(0, 0, 100, 100), 0);

        Assert.Equal(1, context.RenderScaling);
        Assert.Equal(10, context.Snap(10.4), 6);
    }
}
