# Axes, ticks and the window

An axis does two things: it folds data values into a single monotonic number in **unit space**
(`TryToUnit`), and it decides which stretch of that number is on screen (`Resolve`). The first decides where a
point is drawn; the second decides how much is seen. This page covers the five axes, the furniture on them,
and the gestures that move the window.

## The five axes

| Axis | Takes | Unit space | The ladder |
|---|---|---|---|
| `LinearAxis` | numbers | the value itself | 1 / 2 / 2.5 / 5 × 10ⁿ |
| `CategoryAxis` | categories (strings) | the slot | whole slots |
| `DateTimeAxis` | instants | milliseconds since 1970 | clock steps → days → months → years |
| `TimeSpanAxis` | numbers (milliseconds) | the millisecond count | the same clock rules as an instant axis |
| `LogAxis` | numbers | the decimal exponent (100 → 2, 0.1 → −1) | powers of ten, spaced 1/2/5 decades apart |

```csharp
chart.HorizontalAxis = new DateTimeAxis { Title = "Time" };
chart.VerticalAxis = new LinearAxis { Title = "Depth, m" };
```

**The kinds have to match.** When a value is not of a kind the axis understands (an instant handed to a linear
axis, a string to a log axis), `TryToUnit` returns `false` and the point is treated as **missing** on that
dimension: it is not drawn, and it is not moved somewhere that looks plausible instead. That is why "the chart
is blank" is so often an axis and a value that do not go together - the other common cause being a chart with
no theme or no height, which is in [troubleshooting](troubleshooting.md).

**Why unit space exists.** Stacking, sampling, hit testing, selection and zoom are all arithmetic in that one
space, so none of them knows anything about axes: swap the axis and all of it still works. It is also why the
default horizontal value - the position of the record - is a real answer rather than a placeholder.

## The range: Fit and Fixed

```csharp
axis.ScaleMode = AxisScaleMode.Fit;      // the default: fill the plot with the data
axis.Minimum = 0;                        // what to write when zero has to be in view
axis.Maximum = 100;
```

| Mode | Where the range comes from | What zoom and pan write |
|---|---|---|
| `Fit` (the default) | the data; `Minimum`/`Maximum` beat it when they are set | both ends, so the window follows the gesture |
| `Fixed` | `PixelsPerUnit`: one unit of data keeps its size on screen | zoom writes `PixelsPerUnit`, pan writes `Minimum` |

`Fixed` is the real-scale reading: a wider chart shows **more data** rather than stretching the same data
apart. An ECG strip and a trading chart want it. It comes with two concrete rules:

- the grid is **anchored on whole steps** (ticks fall on multiples of `Interval`), the last one may be a
  partial cell, and the grid is drawn all the way to the edge of the plot;
- `Maximum` is **ignored** in `Fixed` - where the window ends is the scale's business - and
  `axis.IsMaximumIgnored` says so, so a host can tell its user instead of leaving them wondering.

`Against Flex`: Flex's axes follow their data, with no fixed pixel scale (the nearest thing there is making
`minimum`/`maximum` chases the data yourself). Stepped zoom - snapping to a set of rungs rather than
free scaling - is not built yet; it is in §14 of the
[design brief](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md).

## Spacing, and where the ticks fall

```csharp
axis.Interval = 10;              // unset, it is chosen for you
axis.MajorGridPitch = 60;        // 60px by default: the gap the automatic choice aims for
axis.MinorGridPitch = 12;        // 12px by default: the fine grid (five of them to a coarse one)
axis.Separators = [0, 25, 50, 75, 100];   // explicit tick positions
```

- **With no `Interval`, the gap is chosen** by aiming at `MajorGridPitch` pixels and then **snapping to a
  readable number** (1 / 2 / 2.5 / 5 × 10ⁿ). The pitch therefore breathes within a factor of two around 60px;
  the 2.5 rung exists for that reason, without it the jump from 2 to 5 is a factor of 2.5.
- **Each axis brings its own ladder.** An instant axis starts at 10µs and walks clock steps (seconds, minutes,
  hours, days), goes to whole days up to a fortnight past a week, then hands over to the calendar's 1/2/3/6
  months, then to whole years (1/2/5/10…, one rung a decade). A category axis only puts ticks on whole slots.
  A log axis only on powers of ten.
- **`Separators`** is the way out when the meaningful values of a domain are known: hand it positions in unit
  space and they replace the generated ticks. Note that **assigning a new list is what notifies** - mutating
  the list in place does not.

## The furniture on an axis

An axis' settings live in two places: **what it covers and how it counts** is on the `IAxis`; **how it is drawn**
is on the `AxisRenderer`. A Cartesian chart hands you both renderers:

```csharp
var x = chart.HorizontalRenderer;      // chart.HorizontalAxis is the axis itself
x.Placement = AxisPlacement.Start;     // the near edge (below or left); End is the far edge
x.TickPlacement = AxisTickPlacement.Outside;
x.TickLength = 4;
x.MinorTickLength = 2;
x.MinorTickPlacement = AxisTickPlacement.Outside;
x.ShowLabels = true;
x.LabelRotation = 45;                  // degrees; the first answer to labels that are too long
x.StaggerLabels = true;                // the second: stand them in two rows
x.FontSize = 11;
x.LabelGap = 4;                        // the air between a tick and its label
x.LabelMargin = 4;                     // the air between the labels and the control's edge
x.TitleGap = 4;                        // the air between the labels and the title
x.MinimumGutter = 56;                  // at least this much room, whatever the labels need (0 by default)
```

| `AxisTickPlacement` | Where the marks are |
|---|---|
| `None` | nowhere: the axis is its line and its labels |
| `Outside` (the default) | outward, taking room in the gutter |
| `Inside` | inward, taking no room |
| `Cross` | across the line, half in and half out |

**Changing any of these asks for a layout pass**, not just a repaint: hiding the labels gives their room back to
the plot, and a title wants a strip of its own. That is what `AxisRenderer.Changed` is for - these settings
move the plot the same way the axis' own settings do.

`MinimumGutter` is there for several charts sharing one axis: the room each leaves is measured from **its own**
labels, so the same moment falls in a different column on each of them. Asking each chart for the widest of
what they left lines the plots up - two lines of arithmetic, worked out on the gallery's Panes page.

## Titles, names and sections

```csharp
axis.Title = "Depth, m";        // written on the chart: what the axis measures
axis.DisplayName = "Depth";     // what a legend or a list calls it; not drawn on the chart
```

A title on a vertical axis reads bottom to top by default (`TitleDirection = AxisTitleDirection.BottomToTop`);
the other way is `TopToBottom`.

**Axis sections** mark out a stretch that has a name and a meaning:

```csharp
chart.HorizontalRenderer.AddSection(new AxisSection
{
    From = startOfNight,
    To = endOfNight,
    Fill = new SolidColorBrush(Color.Parse("#20808080")),
    Name = "Night",
});
```

A named section makes the labels it covers stand aside rather than collide with the name; leave one end unset
and that end runs to the end of the axis.

## Where the text of a label comes from

A tick's text is written by the axis' `FormatValue(unit)`, and some axes also look at the **step**
(`FormatValue(unit, step)`): a line on the first of a month says `2026-11` rather than `2026-11-01`, and a line
on the first of a January says the year. The default ignores the step, so an axis of your own is unaffected.

The three that ship do this:

- `LinearAxis` writes numbers with `G9`. That is not arbitrary: the default `G` writes three accumulations of
  0.2 as `0.6000000000000001`, and a gutter is measured from the widest label - one label like that ate a fifth
  of a 200px-wide chart.
- `DateTimeAxis` writes to the precision the step calls for, and `DisplayOffset` decides which clock the labels
  are read against (the grid and the labels fall in that calendar too).
- `TimeSpanAxis` writes `01:02:03.456`, or `3.01:02:03` past a day, and decides the decimal places from the
  precision of the value when no `Interval` was given.

To change the format, derive an axis and override `FormatValue` (see [extending it](extension.md)) - that is
the only honest place for it, because the gutter is measured from the text after it has been written.

## A second scale

A series may bring an axis of its own (`series.VerticalAxis = new LinearAxis { … }`), and the chart draws it on
the **far edge** with its grid off, one per dimension at most. The rules and an example are on
[series and chart types](series.md#combinations-and-a-second-vertical-scale).

## The window: zoom and pan

Both gestures are on by default: the wheel zooms (**anchored at the pointer**, so the reading under the cursor
stays where it is) and a left-button drag pans (3px before it is a drag). `Shift` restricts either gesture to
the horizontal axis, `Ctrl` to the vertical one. A chart inside a scrolling page usually wants the wheel to
itself off, or the first notch of the wheel is spent on the chart instead of the page:

```csharp
chart.ZoomOnWheel = false;
chart.PanOnDrag = false;
```

The same two gestures are available in code:

```csharp
chart.ZoomBy(1.2, anchor);          // both dimensions
chart.ZoomBy(1.2, 1.0, anchor);     // the horizontal one only
chart.PanBy(dx, dy);                // logical pixels; positive drags the content right and down

Point? at = chart.DataToPoint(x, y);                               // data → control coordinates
(ChartValue X, ChartValue Y)? values = chart.PointToData(point);   // and back
```

The window says when it moves, so a host can update something else beside it:

```csharp
chart.VisibleRangeChanged += (_, e) =>
    status.Text = $"{e.Dimension} {e.Range.Minimum:G4} – {e.Range.Maximum:G4}";
```

The range is in **unit space**, which is what links two charts together without either of them knowing what the
other measures:

```csharp
first.VisibleRangeChanged += (_, e) =>
{
    if (e.Dimension != ChartDimension.X)
    {
        return;
    }

    IAxis axis = second.HorizontalAxis!;
    axis.Minimum = e.Range.Minimum;
    axis.Maximum = e.Range.Maximum;      // in Fit mode both ends are yours to write
};
```

(In `Fixed` mode `Maximum` is ignored, so linking there has to go through `PanBy`/`ZoomBy`.)

`DataToPoint`/`PointToData` are Flex's `dataToLocal`/`localToData`; reach for them when you are drawing
something of your own on a chart.

---

[中文版](../../using/axes.md)
