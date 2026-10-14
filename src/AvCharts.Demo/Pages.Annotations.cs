using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvCharts.Charts;

namespace AvCharts.Demo;

// The page of what is on a chart and is not a series, with the elements that page draws.
public static partial class Pages
{
    /// <summary>The things on a chart that are not series: a band, a threshold, a mark for an event.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Annotations()
    {
        var chart = Chart(0, 8, 0, 10);
        var readings = Line("Readings", [4.0, 6.0, 5.0, 7.0, 6.5, 8.0, 7.5, 9.0]);

        // A band is behind the readings, because the layer it is drawn in is behind them.
        chart.Series.Add(readings);
        chart.Elements.Add(new Band(3, 6));
        chart.Elements.Add(new Threshold(8));
        chart.Elements.Add(new EventMark(5, "released"));

        return Page("Annotations", "The things on a chart that are not series.", Example("A band, a threshold, and a mark for an event", chart));
    }

    /// <summary>A band the readings are meant to stay inside.</summary>
    /// <param name="from">The bottom of the band.</param>
    /// <param name="to">The top of it.</param>
    private sealed class Band(double from, double to) : ChartElement
    {
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(from), ChartValue.FromNumber(from), out var low) ||
                !renderContext.TryPoint(ChartValue.FromNumber(to), ChartValue.FromNumber(to), out var high))
            {
                return;
            }

            var plot = renderContext.PlotBounds;

            context.FillRectangle(
                new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4), 0.12),
                new Rect(plot.Left, high.Y, plot.Width, low.Y - high.Y));
        }
    }

    /// <summary>A line across the plot at a reading nobody is meant to go past.</summary>
    /// <param name="value">The reading.</param>
    private sealed class Threshold(double value) : ChartElement
    {
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(value), ChartValue.FromNumber(value), out var at))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22)), 1, new DashStyle([4, 3], 0));

            context.DrawLine(pen, new Point(plot.Left, at.Y), new Point(plot.Right, at.Y));
        }
    }

    /// <summary>A mark on the plot where something happened.</summary>
    /// <param name="at">The reading it happened at.</param>
    /// <param name="what">What happened.</param>
    private sealed class EventMark(double at, string what) : ChartElement
    {
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(at), ChartValue.FromNumber(at), out var where))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var colour = new SolidColorBrush(Color.FromRgb(0x4A, 0x53, 0x5A));
            var text = new FormattedText(
                what,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                10,
                colour);

            context.DrawLine(new Pen(colour, 1), new Point(where.X, plot.Top), new Point(where.X, plot.Bottom));
            context.DrawText(text, new Point(where.X + 3, plot.Top + 2));
        }
    }
}
