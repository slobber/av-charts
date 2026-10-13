using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class CategoryAxisTests
{
    [Fact]
    public void Slots_are_handed_out_in_the_order_the_categories_are_first_seen()
    {
        var axis = new CategoryAxis();

        Assert.Equal(0, axis.GetSlot("north"));
        Assert.Equal(1, axis.GetSlot("south"));
        Assert.Equal(0, axis.GetSlot("north"));
        Assert.Equal(["north", "south"], axis.Categories);
    }

    [Fact]
    public void A_category_maps_to_its_slot_and_back()
    {
        var axis = new CategoryAxis();

        Assert.True(axis.TryToUnit((ChartValue)"south", out var unit));
        Assert.Equal(0, unit);

        Assert.Equal("south", axis.FromUnit(unit).Category);
    }

    [Fact]
    public void Labels_are_the_names_of_the_categories()
    {
        var axis = new CategoryAxis();
        axis.GetSlot("north");
        axis.GetSlot("south");

        Assert.Equal("north", axis.FormatValue(0));
        Assert.Equal("south", axis.FormatValue(1));

        // Beyond the categories there is no name to give, and a number is better than a wrong one.
        Assert.Equal("4", axis.FormatValue(4));
        Assert.True(axis.FromUnit(4).IsEmpty);
    }

    [Fact]
    public void The_grid_steps_in_whole_slots()
    {
        var axis = new CategoryAxis();

        for (var index = 0; index < 10; index++)
        {
            axis.GetSlot("c" + index);
        }

        var layout = axis.Resolve([new BoundedValue(0), new BoundedValue(9)], 600);

        Assert.Equal(1, layout.MajorInterval);
        Assert.Equal([0d, 1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d], layout.MajorTicks);
    }

    [Fact]
    public void A_few_categories_over_a_wide_plot_still_step_one_at_a_time()
    {
        var axis = new CategoryAxis();
        axis.GetSlot("north");
        axis.GetSlot("south");

        var layout = axis.Resolve([new BoundedValue(0), new BoundedValue(1)], 600);

        Assert.Equal(1, layout.MajorInterval);
        Assert.Equal([0d, 1d], layout.MajorTicks);
    }

    [Fact]
    public void Only_categories_have_slots()
    {
        var axis = new CategoryAxis();

        Assert.False(axis.TryToUnit((ChartValue)1.0, out _));
        Assert.False(axis.TryToUnit(ChartValue.Empty, out _));
    }

    [Fact]
    public void Clearing_starts_the_slots_again()
    {
        var axis = new CategoryAxis();
        axis.GetSlot("north");
        Assert.Equal(1, axis.GetSlot("south"));

        axis.ClearCategories();

        Assert.Empty(axis.Categories);
        Assert.Equal(0, axis.GetSlot("south"));
    }
}
