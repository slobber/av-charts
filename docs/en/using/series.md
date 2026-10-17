# Series and chart types

A chart is one `ChartBase` plus any number of series. The series decide what the readings look like; the chart
decides how the space is laid out and where the axes are drawn. This page is: what each series draws, what it
needs to read, which knobs it has, and how to put several of them on one plot.

## One table

| Series | Draws | Main knobs | Page in the gallery |
|---|---|---|---|
| `LineSeries<T>` | a line | `Stroke`, `StrokeThickness`, `Curve`, the `Marker` family | Lines |
| `AreaSeries<T>` | a line, and the region under it | `Fill` (the region), `Stroke` (the line) | Lines |
| `StepLineSeries<T>` | a line that steps across and up | as a line | Lines |
| `StepAreaSeries<T>` | a stepped area | as an area | Lines |
| `PlotSeries<T>` | marks only (a scatter) | `Marker` (a circle by default), `MarkerSize`, `MarkerFill`, `MarkerStroke` | Points |
| `BubbleSeries<T>` | marks sized by a third reading | `RadiusSelector` / `RadiusPath` | Points |
| `ColumnSeries<T>` | columns | `ColumnWidthRatio`, `MaxColumnWidth`, `Fill`, `Stroke` | Columns |
| `BarSeries<T>` | bars (columns lying down) | as columns | Bars |
| `CandlestickSeries<T>` | candles: a body and two wicks | the `Open`/`High`/`Low`/`Close` selectors, the `Rise…`/`Fall…` inks | Candles |
| `HighLowCloseSeries<T>` | high-low-close bars | as candles | Candles |
| `RangeSeries<T>` | a band between two readings | `LowSelector`, `HighSelector`, `Fill`, `Stroke` | Statistics |
| `ErrorSeries<T>` | an error bar: a mark, a whisker and two caps | `ErrorSelector` | Statistics |
| `BoxSeries<T>` | a box and its whiskers (five numbers) | `MinimumSelector`, `LowerQuartileSelector`, `MedianSelector`, `UpperQuartileSelector`, `MaximumSelector` | Statistics |
| `PieSeries<T>` | a pie: one wedge a reading | `ValueSelector`, `NameSelector`, `InnerRadius`, `StartAngle`, `ExplodeRadius`, the `Label…` family | Pie |

`PlotSeries<T>` is the base of `LineSeries<T>` and `BubbleSeries<T>` (Flex writes the marker code once per
series; here it is written once), `BarSeries<T>` derives from `ColumnSeries<T>` (a bar is a column lying down,
the same way round as Flex), the two financial series derive from `PriceBarSeries<T>`, and the three
statistical ones from `StatisticalSeries<T>`.

## Lines, areas and marks

```csharp
var line = new LineSeries<Reading>
{
    DisplayName = "Depth",
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,

    Stroke = Brushes.SteelBlue,   // without one, the palette's colour for this series
    StrokeThickness = 1.5,
    Curve = CurveMode.Smooth,     // Linear by default: nothing was measured between two readings
    Marker = Markers.Diamond,     // a line has no marks until it is asked for them
    MarkerSize = 4,
};
```

- `CurveMode.Linear` is the default and the honest drawing of two readings with nothing measured between them;
  `Smooth` is a curve through every reading that **stays between neighbouring ones** - it never invents a peak.
- Five markers ship with the library: `Markers.Circle` (the default), `Square`, `Diamond`, `Cross`,
  `Triangle`. `Marker = null` is no marks at all, which is where a line starts. A mark is an interface
  (`IMarkerRenderer`); writing your own is in [extending it](extension.md).
- `AreaSeries<T>` is a line plus a region filled down to the base. **A missing value breaks the region** rather
  than reaching for the nearest valid value (`Against Flex`: Flex's `findClosestValidValue` is refused here on
  purpose - that is inventing a reading).

## Columns, bars, side by side and stacked

```csharp
var sales = new CartesianChart { SeriesLayout = SeriesLayout.Cluster };
sales.Series.Add(new ColumnSeries<Sale> { XSelector = s => s.Month, YSelector = s => s.ThisYear, DisplayName = "This year" });
sales.Series.Add(new ColumnSeries<Sale> { XSelector = s => s.Month, YSelector = s => s.LastYear, DisplayName = "Last year" });
```

The band - how wide one slot is - is worked out by the chart, not by you: a band series declares, per record,
the half-slot of margin that a column needs, which is why the first and last columns of a group are not cut in
half. Three knobs:

| Knob | Default | What it does |
|---|---|---|
| `SeriesLayout` | `Overlay` | `Cluster` side by side · `Overlay` each takes the whole slot · `Stacked` they add up |
| `ColumnWidthRatio` | `0.65` | how much of the slot a column takes (Flex's `columnWidthRatio`) |
| `MaxColumnWidth` | unset | the widest a column may get, so widening the window does not build a brick |

`Overlay` is the default and it is Flex's, which is what makes "a line over a column chart" work: the line
takes no slot and the columns stand in the whole of it. **Only band series take part in the layout** (columns,
bars, candles, high-low-close, ranges, errors, boxes); a line stands in no band.

Stacking (`SeriesLayout.Stacked`) folds the series in the order they are declared: positives pile upwards,
negatives downwards, and the axis grows to cover the tallest pile. A missing value in a stack is drawn with
no height but is **still missing rather than zero** - a data tip can tell the two apart.

`Against Flex`: Flex builds a floating column out of a per-point `minField`. There is no per-point base here;
a base other than zero is stacking (`IStackable`, see [extending it](extension.md)), or drawing that stretch
as a `RangeSeries<T>`.

## Financial: candles and high-low-close

```csharp
var candles = new CandlestickSeries<Bar>
{
    XSelector = bar => bar.Session,
    CloseSelector = bar => bar.Close,     // unset, the record's own value is the close
    OpenSelector = bar => bar.Open,
    HighSelector = bar => bar.High,
    LowSelector = bar => bar.Low,
};
```

A bar that closed at or above where it opened, and one that closed below, are drawn in the theme's two
financial colours (`FinancialUp` / `FinancialDown`); `RiseFill`/`RiseStroke`/`FallFill`/`FallStroke` override
them per series. They come from tokens rather than from palette slots because which of the two a reading wears
is a fact about the reading, not about where the series stands among the others.

`HighLowCloseSeries<T>` is the same information without the body: a line from the low to the high, with a tick
at the open and one at the close.

## Statistics: ranges, error bars, boxes

All three answer the same question - how far apart are these readings - in three shapes:

- **`RangeSeries<T>`** fills a band between two readings (`LowSelector`/`HighSelector`): a confidence interval,
  a tolerance band.
- **`ErrorSeries<T>`** draws a whisker and two caps on a reading, with the error given in **the data's own
  units** (`ErrorSelector`).
- **`BoxSeries<T>`** draws a box and its whiskers from five numbers: minimum, lower quartile, median, upper
  quartile, maximum. The median is the record's own value, and the other four fall back to it when they are
  not given (so a box with no five numbers is a mark).

## Pies and roses

```csharp
var pie = new PieChart { Height = 260 };
pie.Series.Add(new PieSeries<Share>
{
    ValueSelector = share => share.Amount,
    NameSelector = share => share.Name,
    InnerRadius = 0.55,                    // a ring
    StartAngle = 90,                       // the first wedge starts at twelve o'clock
    ExplodeSelector = share => share.IsKey ? 0.08 : 0,
    LabelSelector = share => share.Name,
    LabelPosition = PieLabelPosition.Outside,
});
```

| Property | What it means |
|---|---|
| `ValueSelector` | how much of the whole a reading is (only readings that draw a wedge count towards the total) |
| `NameSelector` | what a wedge is called in a data tip; unset, it is named by where it stands |
| `InnerRadius` / `OuterRadius` | where the wedges start and end, as a **fraction** of the circle's radius; `0.55` is a ring |
| `StartAngle` / `Clockwise` | where the first wedge starts (degrees, nought at three o'clock, growing anticlockwise), and which way they run - anticlockwise by default, as Flex's pie is |
| `ExplodeRadius` | how far the whole ring stands out, as a fraction of the radius; `ExplodeSelector` pushes **one wedge** further, and the two add up |
| `ReserveExplodeRadius` | whether the circle is drawn smaller so a pushed-out wedge still fits (off by default) |
| `RadiusByValue` | the **length** of a wedge carries the reading instead of its width: that is a rose chart |
| `LabelSelector` / `LabelPosition` | what is written on a wedge - `Inside`, or `Outside` beside it with a leader line; nothing is written by default |
| `LabelImageSelector` / `LabelImageSize` | a picture beside a label (a product mark, a browser icon); it is an `IImage`, so the host rasterises anything vector first |

Two things that are easy to misread, both stated in `RadiusByValue`'s own remarks: a rose's radius is a
**linear** proportion, so the *area* of a wedge is not the reading (Nightingale's own diagram scaled the
radius by the square root); and a reading with no value draws no wedge and takes no share of the circle, so
the wedges that are left divide it between them.

A pie's colours come from the palette **per record** (the first record takes the slot the series would have
taken, and the rest follow round), so a pie beside a line does not collide with it. Several pies on one
`PieChart` are nested rings: the chart divides the circle between them (take a look at `SeriesBase.TakeBand`),
so a host does not have to work out `InnerRadius` itself.

## Combinations and a second vertical scale

Several things on one plot is several `Add` calls; the chart does the rest:

```csharp
var chart = new CartesianChart { SeriesLayout = SeriesLayout.Stacked };

chart.Series.Add(new ColumnSeries<Reading> { /* bytes a minute */ });
chart.Series.Add(new LineSeries<Reading>
{
    /* the success rate, in per cent - nothing like the columns' units */
    VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100, Title = "%" },
});
```

A series with an axis of its own (`HorizontalAxis`/`VerticalAxis`, with `SetAxis`/`AxisFor`) is measured and
labelled by **that** axis: that is the second scale. Three rules come with it:

- The chart's own axis is drawn on the near edge (below, or to the left); the first extra axis a series brings
  is drawn on the **far** edge (above, or to the right), which is where a reader looks for it and what keeps
  the two sets of labels from colliding.
- The extra axis's **grid is off**: a second set of grid lines across the same plot says nothing the first
  one has not already said.
- **One extra axis per dimension is drawn.** A third scale is not invented a place: the series measured
  against it are still placed by it, they simply have no labelled axis of their own.

`Against Flex`: a second vertical axis in Flex means putting a renderer into a chart container's
`verticalAxisRenderers`. Here it is "this series uses this axis", and the drawing follows from the chart's own
rules.

`Series.Visible = false` takes a series out of the chart altogether: it is not drawn, it does not grow the
axes, it takes no part in stacking or in a band, an axis it brought is taken away, and no pointer or data tip
can find it. That is what clicking a legend row does (see [looks and theme](formatting.md)).

## One axis instance, several charts

An axis **holds settings and never results** (`Resolve` is a pure function), so one `DateTimeAxis` instance can
serve several charts at once:

```csharp
var across = new DateTimeAxis { Title = "Session" };
price.HorizontalAxis = across;
volume.HorizontalAxis = across;
```

Both charts now show the same stretch of time, and wheeling or dragging one moves the other with it - the axis
holds the settings and each chart resolves its own plot against them.

**The plots themselves need one more step to line up.** Each chart keeps room for **its own** labels down the
side (a price of `110` and a volume of `2,000` are not the same width), so the same moment lands in a different
column on either side and the ticks are out of line. The fix is two lines of arithmetic: read what each chart
actually left, and hand the widest of them back as a floor.

```csharp
// what a chart left is the distance from its edge to its plot
var widest = Math.Max(price.Bounds.X, volume.Bounds.X);
price.VerticalRenderer.MinimumGutter = Math.Max(0, widest - price.Bounds.X);
volume.VerticalRenderer.MinimumGutter = Math.Max(0, widest - volume.Bounds.X);
```

`AxisRenderer.MinimumGutter` (nought by default) is "leave at least this much room, whatever the labels need".
Nothing does it automatically, because a chart does not know it is one of several; the gallery's Panes page is
the worked example (a `Panel` that reads both gutters, sets them, and lays out again, with a knob that shows
what the setting buys).

---

[中文版](../../using/series.md)
