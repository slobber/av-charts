using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Controls;
using iPaas.Controls;

namespace AvCharts.Demo;

/// <summary>
/// The gallery: a page per feature of the library, each one a page a host could put beside its own navigation.
/// </summary>
/// <remarks>
/// <para>
/// The pages are built here rather than written as markup, so that a feature and its example cannot drift apart -
/// and so that the golden tests can render the very pages a reader looks at. Each page is a heading, a line about
/// it, and one example per thing it is showing; the navigation that lists them is <see cref="Catalog"/>.
/// </para>
/// <para>
/// A page is a file of its own - <c>Pages.Lines.cs</c>, <c>Pages.Ecg.cs</c> and the rest - because a page is what
/// a reader comes here to change, and finding it should not mean searching one long file. What every page needs
/// is here beside this declaration; the chart, the series and the readings that more than one page is drawn from
/// are in <c>Pages.Readings.cs</c>; everything else belongs to the page that uses it.
/// </para>
/// </remarks>
public static class Gallery
{
    /// <summary>A page: its name, the line under it, and its examples.</summary>
    /// <param name="title">The name.</param>
    /// <param name="about">The line under it.</param>
    /// <param name="examples">The examples.</param>
    /// <returns>The page.</returns>
    internal static DemoPage Page(string title, string about, params DemoBlock[] examples)
    {
        var stack = new StackPanel { Spacing = 32 };

        foreach (var example in examples)
        {
            stack.Children.Add(example);
        }

        return new DemoPage { Title = title, Description = about, Content = stack };
    }

    /// <summary>One example: what it shows, and the chart itself.</summary>
    /// <param name="title">What the example is called.</param>
    /// <param name="example">The chart.</param>
    /// <returns>The block.</returns>
    /// <remarks>
    /// A chart has no size of its own - it fills what it is given - and a stack has no height to hand out, so
    /// every chart on a page is given one. Note that this is about the charts and not about the panel they are
    /// in: a panel tall enough for its grid is no help at all if the charts inside it are measured with infinite
    /// height, which is what a stack panel gives its children - they then take their own fallback height, spill
    /// out of their cells, and land on top of the next row. That is what the Axes page used to look like.
    /// </remarks>
    internal static DemoBlock Example(string title, Control example)
    {
        GiveHeight(example, ExampleHeight);

        return new DemoBlock { Title = title, Content = example };
    }

    /// <summary>
    /// A playground: the example on the left, the knobs that change it on the right, and a corner to drag.
    /// </summary>
    /// <param name="example">The chart.</param>
    /// <param name="knobs">The knobs, in the order they are stacked.</param>
    /// <returns>What goes in the block.</returns>
    /// <remarks>
    /// <para>
    /// A picture shows what a chart looks like; knobs show what it does. Every page of the gallery has one of
    /// these, because the questions a reader has ("what does a curved line look like here", "what happens if I
    /// turn the grid off") are answered in a second by a switch and not at all by a screenshot.
    /// </para>
    /// <para>
    /// The third question a screenshot cannot answer is what a chart does with the room it is given, so the
    /// example stands in a frame whose bottom-right corner is dragged to resize it. How much of the plot the
    /// axes take, how wide the gutter the labels want, how much of the data fits, and whether a grid keeps its
    /// shape are all answers that only exist at a size.
    /// </para>
    /// </remarks>
    internal static Control Playground(Control example, params Control[] knobs) =>
        Playground(example, ExampleHeight, knobs);

    /// <summary>
    /// A playground whose example starts taller, for the one page whose subject is the paper it is drawn on.
    /// </summary>
    /// <param name="example">The chart.</param>
    /// <param name="height">How tall the frame is to begin with.</param>
    /// <param name="knobs">The knobs, in the order they are stacked.</param>
    /// <returns>What goes in the block.</returns>
    internal static Control Playground(Control example, double height, params Control[] knobs)
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{KnobGap},{KnobWidth}") };
        var panel = new StackPanel { Spacing = 12, Width = KnobWidth };

        foreach (var knob in knobs)
        {
            panel.Children.Add(knob);
        }

        // The frame starts with no width of its own, so that it is exactly as wide as the column it is in - a
        // chart is given a size rather than wanting one, and the frame passes on whatever it is given. A drag
        // gives it a width of its own, and from then on it is that width.
        var frame = new Grid
        {
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // The room the example has: never over the knobs, and never below something a chart can be read in. The
        // room is what the column has left over, so a window made narrower makes the frame give way rather than
        // the knobs - what is being shown is a chart answering to its size, not a page coming apart.
        double Room() => Math.Max(LeastWidth, Math.Min(MostWidth, page.Bounds.Width - KnobWidth - KnobGap));

        var corner = new Thumb
        {
            Name = CornerName,
            Width = 14,
            Height = 14,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner),

            // A thumb is an area for hitting dressed by whatever theme the application wears, and a theme that
            // draws nothing is a corner nobody can take hold of. This one brings a transparent sheet of its own,
            // so the corner works whatever the application's theme says about thumbs.
            Template = new FuncControlTemplate<Thumb>((_, _) => new Border { Background = Brushes.Transparent }),
        };

        corner.DragDelta += (_, e) =>
        {
            // A frame with no width of its own is as wide as its column: that is where a drag starts from.
            var from = double.IsNaN(frame.Width) ? frame.Bounds.Width : frame.Width;

            frame.Width = Math.Clamp(from + e.Vector.X, LeastWidth, Room());
            frame.Height = Math.Clamp(frame.Height + e.Vector.Y, LeastHeight, MostHeight);
        };

        // The corner is drawn as three strokes in a square of its own, which is what tells a reader there is
        // something there to take hold of; the thumb above it is the part that answers the pointer.
        var grip = new Canvas
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
        };

        for (var stroke = 0; stroke < 3; stroke++)
        {
            grip.Children.Add(new Line
            {
                StartPoint = new Point(15 - (stroke * 5), 1),
                EndPoint = new Point(1, 15 - (stroke * 5)),
                Stroke = GripBrush,
                StrokeThickness = 1.4,
            });
        }

        frame.Children.Add(example);
        frame.Children.Add(grip);
        frame.Children.Add(corner);

        // How big the example is, written down: a reader watching the chart answer to a size wants to know which
        // size they are looking at, and the number is what makes the drag legible.
        var size = new TextBlock { FontSize = 11, Opacity = 0.6, Text = "Drag the corner to resize." };

        frame.SizeChanged += (_, e) =>
            size.Text = $"Drag the corner to resize: this one is {e.NewSize.Width:0} by {e.NewSize.Height:0}.";

        // The window can be made narrower than the frame, and then the frame is what gives way: the knobs stay
        // where they are, and the corner stays where it can be reached.
        page.SizeChanged += (_, _) =>
        {
            if (!double.IsNaN(frame.Width))
            {
                frame.Width = Math.Min(frame.Width, Room());
            }
        };

        var stage = new StackPanel { Spacing = 6 };

        stage.Children.Add(frame);
        stage.Children.Add(size);

        Grid.SetColumn(stage, 0);
        Grid.SetColumn(panel, 2);
        page.Children.Add(stage);
        page.Children.Add(panel);
        return page;
    }

    /// <summary>
    /// One example that is a playground: it sizes itself, so nothing here gives it a height.
    /// </summary>
    /// <param name="title">What the example is called.</param>
    /// <param name="content">The playground.</param>
    /// <returns>The block.</returns>
    internal static DemoBlock Try(string title, Control content) => new() { Title = title, Content = content };

    /// <summary>Runs something while a page is on screen, and stops it when the page is put away.</summary>
    /// <param name="control">The control whose being on screen is the question.</param>
    /// <param name="live">What to do while it is: start a clock, let a feed arrive.</param>
    /// <param name="quiet">What to do when it is put away: stop the clock, hold the feed.</param>
    /// <remarks>
    /// A page that keeps a clock running while the reader is looking at another page costs something for nothing,
    /// and a running timer is held by the dispatcher - so the page it belongs to cannot be collected while nobody
    /// can see it. The gallery's two live pages are built with this.
    /// </remarks>
    internal static void WhileShown(Control control, Action live, Action quiet)
    {
        control.AttachedToVisualTree += (_, _) => live();
        control.DetachedFromVisualTree += (_, _) => quiet();
    }

    /// <summary>A knob that picks one of a few named settings.</summary>
    /// <typeparam name="T">What the settings are.</typeparam>
    /// <param name="label">What the knob is called.</param>
    /// <param name="options">The settings, in the order they are offered.</param>
    /// <param name="selected">Which one is picked to begin with.</param>
    /// <param name="onPick">What to do when another one is picked.</param>
    /// <returns>The knob.</returns>
    internal static Control Choice<T>(string label, IReadOnlyList<T> options, T selected, Action<T> onPick)
    {
        // The house's own select, so the demo's chrome looks like the rest of the house's demos.
        var picker = new Select
        {
            ItemsSource = options,
            SelectedItem = selected,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is T picked)
            {
                onPick(picked);
            }
        };

        return Labelled(label, picker);
    }

    /// <summary>A knob that turns something on or off.</summary>
    /// <param name="label">What the knob is called.</param>
    /// <param name="on">Whether it starts on.</param>
    /// <param name="onChange">What to do when it changes.</param>
    /// <returns>The knob.</returns>
    internal static Control OnOff(string label, bool on, Action<bool> onChange)
    {
        var pill = new Switch { IsChecked = on };

        pill.IsCheckedChanged += (_, _) => onChange(pill.IsChecked == true);

        // The switch is the pill and nothing else - the house says so in its own demo - so the name is a row of
        // its own beside it.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        row.Children.Add(pill);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>A row of choices read as one control, which is what the house's toggle group is for.</summary>
    /// <param name="label">What the knob is called.</param>
    /// <param name="options">The choices, in the order they are offered.</param>
    /// <param name="selected">Which one is on to begin with.</param>
    /// <param name="onPick">What to do when another one is turned on.</param>
    /// <returns>The knob.</returns>
    /// <remarks>
    /// A group of toggles does not manage its own selection - the group is the well they sit in - so the rule
    /// that one of them is on lives here: turning one on turns the rest off, and the one that is on cannot be
    /// turned off by clicking it again.
    /// </remarks>
    internal static Control Choices(string label, IReadOnlyList<string> options, int selected, Action<int> onPick)
    {
        // A row that wraps rather than one that runs off the side: a group of three or four buttons can be wider
        // than the column the knobs stand in, and a choice nobody can read is not a choice. The width is given
        // rather than left to the room, because the group measures its content with no limit - so a wrapping row
        // that was not told how wide it is would never wrap.
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Width = KnobWidth };
        var toggles = new List<Toggle>();

        // Turning the others off is a change to them, and their own handlers would turn *them* back on - which
        // turns this one off, which turns it back on, and so on until the stack runs out. A guard says which
        // changes this code is making and which the reader is: only a change a reader made goes on to be acted
        // on, and the ones made here are left alone. (This is not a hypothetical: without it, clicking one of
        // these buttons threw a StackOverflowException and took the application with it.)
        var changing = false;

        for (var index = 0; index < options.Count; index++)
        {
            var at = index;
            var toggle = new Toggle { Content = options[index], IsChecked = index == selected, Size = ToggleSize.Sm };

            toggle.IsCheckedChanged += (_, _) =>
            {
                if (changing)
                {
                    return;
                }

                changing = true;

                try
                {
                    if (toggle.IsChecked != true)
                    {
                        // A group read as one control always has one of them on: clicking the one that is on
                        // leaves it on.
                        toggle.IsChecked = true;
                    }
                    else
                    {
                        foreach (var other in toggles.Where(other => !ReferenceEquals(other, toggle)))
                        {
                            other.IsChecked = false;
                        }

                        onPick(at);
                    }
                }
                finally
                {
                    changing = false;
                }
            };

            toggles.Add(toggle);
            row.Children.Add(toggle);
        }

        return Labelled(label, new ToggleGroup { Content = row });
    }

    /// <summary>Puts a name above a knob, which is how the house's demos label them.</summary>
    /// <param name="label">The name.</param>
    /// <param name="knob">The knob.</param>
    /// <returns>The two together.</returns>
    internal static Control Labelled(string label, Control knob)
    {
        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold });
        stack.Children.Add(knob);
        return stack;
    }

    /// <summary>Gives every chart in an example a height, wherever it is nested.</summary>
    /// <param name="control">The example, or something inside it.</param>
    /// <param name="height">The height to give a chart that has none.</param>
    /// <remarks>
    /// A chart has no size of its own: it fills what it is given, and what a stack panel gives is unlimited
    /// height - so a chart left to itself takes its own fallback height and spills out of whatever was supposed
    /// to hold it. Every chart in the gallery is therefore given one, however deep it is.
    /// </remarks>
    private static void GiveHeight(Control control, double height)
    {
        switch (control)
        {
            case ChartBase chart:
                if (double.IsNaN(chart.Height))
                {
                    chart.Height = height;
                }

                break;

            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>())
                {
                    GiveHeight(child, height);
                }

                break;

            case Decorator decorator when decorator.Child is { } child:
                GiveHeight(child, height);
                break;

            case ContentControl content when content.Content is Control inner:
                GiveHeight(inner, height);
                break;

            default:
                break;
        }
    }

    /// <summary>The strokes of a playground's corner: dark enough to read on the light page the gallery is drawn on.</summary>
    private static readonly IBrush GripBrush = new SolidColorBrush(Color.FromArgb(0x99, 0x40, 0x48, 0x50));

    /// <summary>How tall an example is drawn, so that a page is the same shape wherever it is shown.</summary>
    private const double ExampleHeight = 240;

    /// <summary>The name of a playground's corner, so that a test can take hold of the same one a reader does.</summary>
    public const string CornerName = "playground-corner";

    /// <summary>How wide the knobs of a playground are, and the gap between them and the example.</summary>
    private const double KnobWidth = 220;

    /// <summary>The gap between a playground's example and its knobs.</summary>
    private const double KnobGap = 16;

    /// <summary>The smallest a playground's example may be dragged, and the largest it is worth dragging.</summary>
    private const double LeastWidth = 220;

    /// <summary>How wide a playground's example may be dragged, whatever room there is.</summary>
    private const double MostWidth = 900;

    /// <summary>How short a playground's example may be dragged.</summary>
    private const double LeastHeight = 140;

    /// <summary>How tall a playground's example may be dragged.</summary>
    private const double MostHeight = 600;

    /// <summary>How tall a chart in a cell of a page of several is drawn.</summary>
    internal const double CellHeight = 150;

    /// <summary>
    /// How tall the strip chart is: three units of signal over a plot of two hundred and forty pixels is eighty
    /// pixels a unit, and at forty pixels a second one small square of the grid is eight pixels either way.
    /// </summary>
    internal const double SquareGridHeight = 272;
}
