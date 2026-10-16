using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the library's defaults: the theme and the face a host sets once instead of on every chart.
/// </summary>
/// <remarks>
/// The defaults are the library's own state rather than a chart's, so every test here puts them back the way it
/// found them - a test that left a theme behind would dress the charts of whichever test ran next.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ChartDefaultsTests
{
    [Fact]
    public void A_chart_built_after_the_defaults_are_set_is_dressed_by_them()
    {
        // The tree carries no tokens, so a chart is bare - and the one line a host writes at startup is the whole
        // of what it takes to dress every chart it builds afterwards.
        var before = Chart();

        _ = new Window { Content = before };

        Layout(before);

        Assert.Equal(0, Ink.Count(Picture(before)));
        Assert.Null(before.Series[0].Color);

        try
        {
            ChartDefaults.Configure(defaults => defaults.Theme = ChartTheme.Create());

            var after = Chart();

            _ = new Window { Content = after };

            Layout(after);

            Assert.True(Ink.Count(Picture(after)) > 0, "the chart is still undressed");
            Assert.NotNull(after.Series[0].Color);

            // And the defaults are for the charts built from here on: one that is already drawn keeps what it was
            // dressed with, which is the same rule as any other theme - the library does not reach into a chart it
            // has handed out.
            Assert.Equal(0, Ink.Count(Picture(before)));
        }
        finally
        {
            ChartDefaults.Configure(defaults => defaults.Theme = null);
        }
    }

    [Fact]
    public void The_theme_a_chart_can_see_is_read_before_the_defaults()
    {
        // Two themes that cannot be confused: the defaults hold a palette of the host's, and one of the two
        // charts has the library's theme in its own tree. Whoever is dressed with what is read off the series'
        // colour - which is the colour it is drawn in, so this is the picture's own answer.
        var mine = new SolidColorBrush(Color.FromRgb(0x11, 0x88, 0xEE));
        var library = ChartTheme.Create();

        try
        {
            ChartDefaults.Configure(defaults => defaults.Theme = Palette(mine));

            var bare = InABareWindow();
            var dressed = InAWindowWith(library);

            Assert.Equal(mine.Color, Colour(bare));
            Assert.Equal(Token(library, ChartTokens.SeriesStroke(1)), Colour(dressed));

            // Set again, and the charts built after that follow: the defaults are read when a chart is built, so
            // changing them is changing what the next chart starts from.
            var other = new SolidColorBrush(Color.FromRgb(0x99, 0x22, 0x44));

            ChartDefaults.Configure(defaults => defaults.Theme = Palette(other));

            Assert.Equal(other.Color, Colour(InABareWindow()));
        }
        finally
        {
            ChartDefaults.Configure(defaults => defaults.Theme = null);
        }
    }

    [Fact]
    public void A_face_configured_once_is_what_the_axes_are_lettered_in()
    {
        var mine = new Typeface(FontFamily.Parse("Georgia"));
        var theirs = new Typeface(FontFamily.Parse("Consolas"));

        // Untouched, an axis is lettered in the library's own face - and the library's own face is what a chart
        // wears when nothing has been configured, whether it was configured after that chart or not.
        var before = Chart();

        Assert.Equal(new Typeface(FontFamily.Default), before.VerticalRenderer.Typeface);

        try
        {
            ChartDefaults.Configure(defaults => defaults.Typeface = mine);

            var chart = Chart();

            Assert.Equal(mine, chart.VerticalRenderer.Typeface);
            Assert.Equal(mine, chart.HorizontalRenderer.Typeface);

            // An axis given a face of its own keeps it - one chart lettered differently is a chart, not a
            // violation of the defaults.
            chart.VerticalRenderer.Typeface = theirs;

            Assert.Equal(theirs, chart.VerticalRenderer.Typeface);
            Assert.Equal(mine, chart.HorizontalRenderer.Typeface);
        }
        finally
        {
            ChartDefaults.Configure(defaults => defaults.Typeface = null);
        }
    }

    /// <summary>A chart in a window that carries no theme of its own.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart InABareWindow()
    {
        var chart = Chart();

        _ = new Window { Content = chart };
        return chart;
    }

    /// <summary>A chart in a window dressed with a theme of its own.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart InAWindowWith(ResourceDictionary theme)
    {
        var chart = Chart();

        _ = new Window { Resources = theme, Content = chart };
        return chart;
    }

    [Fact]
    public void The_defaults_are_read_not_replaced()
    {
        Assert.NotNull(ChartDefaults.Current);
        Assert.Throws<ArgumentNullException>(() => ChartDefaults.Configure(null!));
    }

    /// <summary>A chart with a line on it, which is enough to be dressed and to have a colour.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
        };

        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = [1.0, 5.0, 3.0, 8.0],
            YSelector = value => value,
            StrokeThickness = 2,
        });

        return chart;
    }

    /// <summary>A theme holding one palette slot, which is the whole of what a chart needs to have a colour.</summary>
    /// <param name="brush">The colour.</param>
    /// <returns>The theme.</returns>
    private static ResourceDictionary Palette(IBrush brush) =>
        new() { [ChartTokens.SeriesStroke(1)] = new Pen(brush, 2) };

    /// <summary>The colour a chart's first series is drawn in, which is what the theme decides.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The colour.</returns>
    private static Color Colour(CartesianChart chart) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsAssignableFrom<SeriesBase>(chart.Series[0]).Color).Color;

    /// <summary>One token of a theme as the colour it is, read in the light variant.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token's key.</param>
    /// <returns>The colour.</returns>
    private static Color Token(ResourceDictionary theme, string token)
    {
        Assert.True(theme.TryGetResource(token, ThemeVariant.Light, out var value), $"{token} is not defined");

        return Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsAssignableFrom<Pen>(value).Brush).Color;
    }

    /// <summary>Lays a chart out, which is what it needs before it is drawn at all.</summary>
    /// <param name="chart">The chart.</param>
    private static void Layout(CartesianChart chart)
    {
        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));
    }

    /// <summary>Draws a chart, so that a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200));
        bitmap.Render(chart);
        return bitmap;
    }
}
// diagnostic marker
