using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks how the chart hands its palette out: in declaration order, skipping series that bring their own
/// colours, and again from scratch when the theme under it changes.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class ChartPaletteTests
{
    [Fact]
    public void Series_take_the_palette_in_the_order_they_were_added()
    {
        var themed = new ThemedChart();
        var first = Line();
        var second = Line();
        var third = Line();

        themed.Chart.Series.Add(first);
        themed.Chart.Series.Add(second);
        themed.Chart.Series.Add(third);

        Assert.Same(themed.Chart.PaletteStroke(0), first.PaletteStroke);
        Assert.Same(themed.Chart.PaletteStroke(1), second.PaletteStroke);
        Assert.Same(themed.Chart.PaletteStroke(2), third.PaletteStroke);
        Assert.Same(themed.Chart.PaletteFill(1), second.PaletteFill);

        // And the palette is the theme's own pens and brushes, not copies of them.
        Assert.Same(themed.Value(ChartTokens.SeriesStroke(1), ThemeVariant.Light), themed.Chart.PaletteStroke(0));
        Assert.Same(themed.Value(ChartTokens.SeriesFill(3), ThemeVariant.Light), third.PaletteFill);
    }

    [Fact]
    public void A_series_with_a_colour_of_its_own_takes_no_slot()
    {
        var themed = new ThemedChart();
        var byHand = Line(Brushes.HotPink);
        var plain = Line();

        themed.Chart.Series.Add(byHand);
        themed.Chart.Series.Add(plain);

        // The first series kept its own colour, and the second still gets the first slot: this is the Flex
        // behaviour the design brief calls out, where setting one series' colour shifted every series after it.
        Assert.Null(byHand.PaletteStroke);
        Assert.Null(byHand.PaletteFill);
        Assert.Same(themed.Chart.PaletteStroke(0), plain.PaletteStroke);
    }

    [Fact]
    public void Giving_a_series_a_colour_later_moves_the_others_up()
    {
        var themed = new ThemedChart();
        var first = Line();
        var second = Line();
        var third = Line();

        themed.Chart.Series.Add(first);
        themed.Chart.Series.Add(second);
        themed.Chart.Series.Add(third);

        second.Stroke = Brushes.HotPink;

        Assert.Same(themed.Chart.PaletteStroke(0), first.PaletteStroke);
        Assert.Null(second.PaletteStroke);
        Assert.Same(themed.Chart.PaletteStroke(1), third.PaletteStroke);
    }

    [Fact]
    public void The_palette_cycles_when_there_are_more_series_than_colours()
    {
        var themed = new ThemedChart();
        var series = new List<LineSeries<double>>();

        for (var index = 0; index <= ChartTokens.PaletteSize; index++)
        {
            var line = Line();
            series.Add(line);
            themed.Chart.Series.Add(line);
        }

        Assert.Same(series[0].PaletteStroke, series[ChartTokens.PaletteSize].PaletteStroke);

        // An index past the end of the palette comes back around, which is what a pie chart handing in a point
        // index expects.
        Assert.Same(themed.Chart.PaletteStroke(0), themed.Chart.PaletteStroke(ChartTokens.PaletteSize));
        Assert.Same(themed.Chart.PaletteFill(1), themed.Chart.PaletteFill(ChartTokens.PaletteSize + 1));
    }

    [Fact]
    public void A_variant_change_repaints_the_series_in_the_other_palette()
    {
        var themed = new ThemedChart();
        var series = Line();

        themed.Chart.Series.Add(series);

        var light = Assert.IsType<Pen>(themed.Value(ChartTokens.SeriesStroke(1), ThemeVariant.Light));
        var dark = Assert.IsType<Pen>(themed.Value(ChartTokens.SeriesStroke(1), ThemeVariant.Dark));

        Assert.Same(light, series.PaletteStroke);
        Assert.True(Painted(themed.Chart, light) > 0, "the line should be drawn in the light palette's colour");

        // The same chart, the same layers, the same series: only the variant under it changes.
        themed.Window.RequestedThemeVariant = ThemeVariant.Dark;

        Assert.Same(dark, series.PaletteStroke);
        Assert.True(Painted(themed.Chart, dark) > 0, "and then in the dark palette's colour");
        Assert.Equal(0, Painted(themed.Chart, light));
    }

    /// <summary>A line series of three points, in a colour of its own when one is given.</summary>
    private static LineSeries<double> Line(IBrush? stroke = null) =>
        new() { ItemsSource = [1.0, 2.0, 3.0], YSelector = value => value, Stroke = stroke };

    /// <summary>How many pixels a chart draws in a pen's colour.</summary>
    private static int Painted(Control chart, Pen pen)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200));
        bitmap.Render(chart);

        return Ink.Count(bitmap, Assert.IsAssignableFrom<ISolidColorBrush>(pen.Brush).Color);
    }
}
