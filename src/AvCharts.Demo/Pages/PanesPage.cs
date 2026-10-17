using Avalonia;
using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The Panes page: two charts on one axis, with their plots lined up.
internal static class PanesPage
{
    /// <summary>The room between the two panes, in logical pixels.</summary>
    private const double Gap = 6;

    /// <summary>A price and its volume, one above the other, on one time axis.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create() =>
        Page(
            "Panes",
            "Two charts on one time axis, with their plots lined up.",
            Example("A price above its volume, on one axis", Two(LineUp: true, out _)),
            Try("Try it: the same two charts, lined up or each with its own room", PanesPlayground()));

    /// <summary>The same two panes with the alignment on a knob.</summary>
    /// <returns>The playground.</returns>
    private static Control PanesPlayground()
    {
        var panes = Two(LineUp: true, out var board);

        // Setting the property is the whole of it: the panel asks for a layout pass of its own, because only it
        // knows that its children are laid out differently now.
        void Align(string picked) => panes.LineUp = picked == "lined up";

        return Playground(
            panes,
            Choice("Plots", ["lined up", "each its own"], "lined up", Align));
    }

    /// <summary>The two charts, in the panel that keeps their plots in the same columns.</summary>
    /// <param name="LineUp">Whether the plots are made to line up to begin with.</param>
    /// <param name="board">The panel, so that a knob can ask for it to be laid out again.</param>
    /// <returns>The panel.</returns>
    private static Panes Two(bool LineUp, out Panes board)
    {
        var sessions = Sessions();

        // One axis instance, handed to both charts: which is what makes the two show the same stretch of time, and
        // what makes panning or zooming one of them pan or zoom the other - the axis holds the settings and both
        // charts resolve their own plot against them.
        var across = new DateTimeAxis { Title = "Session" };

        var price = new CartesianChart
        {
            HorizontalAxis = across,
            VerticalAxis = new LinearAxis { Title = "Price" },
            Height = 150,
        };

        price.Series.Add(new LineSeries<Session>
        {
            DisplayName = "Close",
            ItemsSource = sessions,
            XSelector = session => session.Day,
            YSelector = session => session.Close,
            StrokeThickness = 2,
        });

        var volume = new CartesianChart
        {
            HorizontalAxis = across,
            VerticalAxis = new LinearAxis { Title = "Volume", Minimum = 0 },
            Height = 90,
        };

        volume.Series.Add(new ColumnSeries<Session>
        {
            DisplayName = "Volume",
            ItemsSource = sessions,
            XSelector = session => session.Day,
            YSelector = session => session.Volume,
        });

        board = new Panes { LineUp = LineUp };
        board.Children.Add(price);
        board.Children.Add(volume);
        return board;
    }

    /// <summary>Twenty sessions of a price, each with the volume that changed hands.</summary>
    /// <returns>The sessions, oldest first.</returns>
    private static Session[] Sessions()
    {
        var first = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero);
        var sessions = new List<Session>();
        var price = 104.0;

        for (var day = 0; day < 20; day++)
        {
            var close = Math.Round(price + (5 * Math.Sin(day * 0.8)) + (1.5 * Math.Sin(day * 2.3)), 2);
            var volume = Math.Round(1800 + (900 * Math.Cos(day * 1.1)), 0);

            sessions.Add(new Session(first.AddDays(day), close, volume));
            price = close;
        }

        return [.. sessions];
    }

    /// <summary>One session: the day, the price it closed at, and the volume that changed hands.</summary>
    /// <param name="Day">The day.</param>
    /// <param name="Close">The closing price.</param>
    /// <param name="Volume">The volume.</param>
    private sealed record Session(DateTimeOffset Day, double Close, double Volume);

    /// <summary>
    /// Two charts in a column whose plots start and end in the same columns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two charts share the time axis, so the same moment is the same fraction of the way across each plot -
    /// but each chart leaves room for its own labels down the side, and a price of a hundred and four is wider
    /// than a volume of two thousand. So the plots begin in different columns and the ticks do not line up.
    /// </para>
    /// <para>
    /// Lining them up is two lines of arithmetic, done here rather than by the library: lay the children out once,
    /// read what each of them ended up leaving, ask both for the wider of the two through
    /// <see cref="AxisRenderer.MinimumGutter"/>, and lay them out again. The second look is the same one a chart
    /// takes at its own gutter - the room the labels need depends on the plot, and the plot on the room - and one
    /// pass of it is enough, because the widest of the two cannot grow by being asked for it.
    /// </para>
    /// </remarks>
    private sealed class Panes : Panel
    {
        private bool _lineUp = true;

        /// <summary>Whether the plots are made to line up; on to begin with.</summary>
        /// <remarks>
        /// A measure invalidation rather than an arrangement one, and that is not a detail: invalidating an
        /// arrangement tells a control's children to lay themselves out again but says nothing to its parents, and
        /// a parent whose own arrangement is still good never asks it to arrange itself at all. This travels up,
        /// which is what gets the panel's own arrange - the pass that reads the gutters - to run again.
        /// </remarks>
        public bool LineUp
        {
            get => _lineUp;
            set
            {
                if (_lineUp == value)
                {
                    return;
                }

                _lineUp = value;

                // Asked for at once rather than at the next layout pass: what the property means is which room the
                // charts are given, and a chart handed different room lays itself out again on its own account. The
                // invalidation is for the panel itself - the children have moved by different amounts, so the
                // panel's own arrangement is stale.
                Align();
                InvalidateMeasure();
            }
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            var height = 0d;

            foreach (var child in Children)
            {
                child.Measure(new Size(width, availableSize.Height));
                height += child.DesiredSize.Height;
            }

            return new Size(width, height + (Gap * Math.Max(0, Children.Count - 1)));
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            Stack(finalSize);

            Align();

            // Laid out again whatever was asked for: asking a chart for different room is a change to the chart,
            // and the pass that turns it into a plot is the one that arranges it - which has just been asked for
            // and has not happened yet.
            Stack(finalSize);

            return finalSize;
        }

        /// <summary>
        /// Asks every chart for the room the widest of them left, or for none of it.
        /// </summary>
        /// <remarks>
        /// In this panel's own coordinates - the left of the plot rather than of the control - since that is the
        /// column a moment falls in. A chart narrower than the widest is asked for the difference, through the
        /// axis' own <see cref="AxisRenderer.MinimumGutter"/>; keeping its own room is asking it for nothing.
        /// </remarks>
        private void Align()
        {
            var widest = LineUp
                ? Children
                    .OfType<CartesianChart>()
                    .Select(chart => chart.Bounds.X + chart.PlotBounds.X)
                    .DefaultIfEmpty(0)
                    .Max()
                : 0;

            foreach (var chart in Children.OfType<CartesianChart>())
            {
                chart.VerticalRenderer.MinimumGutter = Math.Max(0, widest - chart.Bounds.X);
            }
        }

        /// <summary>Puts the children in a column, one under the other.</summary>
        /// <param name="finalSize">The size the panel was given.</param>
        private void Stack(Size finalSize)
        {
            var top = 0d;

            foreach (var child in Children)
            {
                var height = Math.Max(0, child.DesiredSize.Height);

                child.Arrange(new Rect(0, top, finalSize.Width, height));
                top += height + Gap;
            }
        }
    }
}
