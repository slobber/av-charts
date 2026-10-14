using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvCharts.Charts;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The page of what is on a chart and is not a series, with the elements that page draws.
internal static class AnnotationsPage
{
    /// <summary>The things on a chart that are not series: a band, a threshold, a mark for an event.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var chart = Chart(0, 8, 0, 10);
        var readings = Line("Readings", [4.0, 6.0, 5.0, 7.0, 6.5, 8.0, 7.5, 9.0]);

        // A band is behind the readings, because the layer it is drawn in is behind them.
        chart.Series.Add(readings);
        chart.Elements.Add(new Band { From = 3, To = 6 });
        chart.Elements.Add(new Threshold { Value = 8 });
        chart.Elements.Add(new EventMark { At = 5, What = "released" });

        return Page(
            "Annotations",
            "The things on a chart that are not series.",
            Example("A band, a threshold, and a mark for an event", chart),
            Try("Try it: the band, the threshold and the mark, where you want them", AnnotationPlayground()));
    }

    /// <summary>
    /// The same chart with a knob each for the band, the threshold and the mark.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// An element of a host's own is added to and taken out of <see cref="AvCharts.Controls.ChartBase.Elements"/>
    /// like any other thing a chart holds, and an element that moves says so itself - which is why none of these
    /// knobs has to know anything about layers or repainting.
    /// </remarks>
    private static Control AnnotationPlayground()
    {
        var chart = Chart(0, 8, 0, 10);

        chart.Series.Add(Line("Readings", [4.0, 6.0, 5.0, 7.0, 6.5, 8.0, 7.5, 9.0]));

        var band = new Band { From = 3, To = 6 };
        var threshold = new Threshold { Value = 8 };
        var mark = new EventMark { At = 5, What = "released" };

        chart.Elements.Add(band);
        chart.Elements.Add(threshold);
        chart.Elements.Add(mark);

        void Band_(int picked)
        {
            band.From = picked == 0 ? 3 : 6;
            band.To = picked == 0 ? 6 : 9;
            Show(band, picked < 2);
        }

        void Threshold_(int picked)
        {
            threshold.Value = picked switch { 0 => 7, 1 => 8, _ => 9 };
            Show(threshold, picked < 3);
        }

        void Mark(int picked)
        {
            mark.At = picked switch { 0 => 2, 1 => 5, _ => 7 };
            mark.What = picked switch { 0 => "started", 1 => "released", _ => "stopped" };
        }

        void Show(ChartElement element, bool wanted)
        {
            if (wanted && !chart.Elements.Contains(element))
            {
                chart.Elements.Add(element);
            }

            if (!wanted)
            {
                chart.Elements.Remove(element);
            }
        }

        return Playground(
            chart,
            Choices("Band", ["3 to 6", "6 to 9", "none"], 0, Band_),
            Choices("Threshold", ["7", "8", "9", "none"], 1, Threshold_),
            Choices("Event", ["2 started", "5 released", "7 stopped"], 1, Mark));
    }

    /// <summary>A band the readings are meant to stay inside.</summary>
    private sealed class Band : ChartElement
    {
        private double _from = 3;
        private double _to = 6;

        /// <summary>The bottom of the band.</summary>
        public double From
        {
            get => _from;
            set
            {
                if (_from.Equals(value))
                {
                    return;
                }

                _from = value;
                Invalidate();
            }
        }

        /// <summary>The top of it.</summary>
        public double To
        {
            get => _to;
            set
            {
                if (_to.Equals(value))
                {
                    return;
                }

                _to = value;
                Invalidate();
            }
        }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(_from), ChartValue.FromNumber(_from), out var low) ||
                !renderContext.TryPoint(ChartValue.FromNumber(_to), ChartValue.FromNumber(_to), out var high))
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
    private sealed class Threshold : ChartElement
    {
        private double _value = 8;

        /// <summary>The reading the line is drawn at.</summary>
        public double Value
        {
            get => _value;
            set
            {
                if (_value.Equals(value))
                {
                    return;
                }

                _value = value;
                Invalidate();
            }
        }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(_value), ChartValue.FromNumber(_value), out var at))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22)), 1, new DashStyle([4, 3], 0));

            context.DrawLine(pen, new Point(plot.Left, at.Y), new Point(plot.Right, at.Y));
        }
    }

    /// <summary>A mark on the plot where something happened.</summary>
    private sealed class EventMark : ChartElement
    {
        private double _at = 5;
        private string _what = "released";

        /// <summary>The reading it happened at.</summary>
        public double At
        {
            get => _at;
            set
            {
                if (_at.Equals(value))
                {
                    return;
                }

                _at = value;
                Invalidate();
            }
        }

        /// <summary>What happened, written beside the line.</summary>
        public string What
        {
            get => _what;
            set
            {
                if (_what == value)
                {
                    return;
                }

                _what = value;
                Invalidate();
            }
        }

        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(_at), ChartValue.FromNumber(_at), out var where))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var colour = new SolidColorBrush(Color.FromRgb(0x4A, 0x53, 0x5A));
            var text = new FormattedText(
                _what,
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
