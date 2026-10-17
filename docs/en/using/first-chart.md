# A first chart

This page walks the whole of "nothing to a chart": install it, dress it, draw something, put it on a page,
take a picture of it. Each step says why it is shaped the way it is, because the three things that trip people
up here - the height, the theme, the selectors - are also what every later page refers back to.

## 1. Install it

```pwsh
dotnet add package AvCharts.Controls
```

`AvCharts.Controls` depends on `AvCharts`, so both arrive. The split means something:

- **`AvCharts`** is the model: axes, transforms, series, the drawing kernel, the geometry batch. It uses
  Avalonia's **base half only** (`Avalonia.Base`) and knows nothing of `Avalonia.Controls`, so it can be
  driven on a machine with no UI, in a test, or from another host.
- **`AvCharts.Controls`** is the control half: `ChartBase : Control`, `CartesianChart`, `PieChart`, data tips,
  legends, scroll bars, the overview, the control themes and the colour tokens.

## 2. Dress it

Every colour a chart draws comes from a **token**, the tokens live in one resource dictionary with a light and
a dark set, and there are two ways to bring it.

In XAML, one line, and every chart on the page is dressed:

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

In code, where a host builds its resources itself, `ChartTheme.Create()`:

```csharp
Application.Current!.Resources.MergedDictionaries.Add(ChartTheme.Create());
```

A host with many windows, which would rather not repeat either of those, has a **one-line global default** -
set once in `Main` or `App`, and every chart built afterwards starts from it:

```csharp
ChartDefaults.Configure(defaults =>
{
    defaults.Theme = ChartTheme.Create();
    defaults.Typeface = new Typeface("Inter");
});
```

Two things worth knowing:

- The `Typeface` is for **axis labels**, which a renderer draws with no control above it to inherit a font
  from. A chart with Chinese labels needs a face that has them (the gallery's browser build carries a subset
  of Noto Sans SC for exactly this reason - see `DemoFonts`).
- It is a **startup** call. A chart already on screen keeps what it was dressed with until its own resources
  change, which is the same rule as any other theme.

## 3. Draw something

In code:

```csharp
using AvCharts.Axes;      // axes
using AvCharts.Controls;  // the chart control
using AvCharts.Series;    // series

var chart = new CartesianChart
{
    Height = 240,
    HorizontalAxis = new DateTimeAxis { Title = "Time" },
    VerticalAxis = new LinearAxis { Title = "Depth, m" },
};

chart.Series.Add(new LineSeries<Reading>
{
    DisplayName = "Depth",
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
});
```

A `CartesianChart` starts with a `LinearAxis` on each dimension, so writing only the one you want to change is
enough.

The same chart in XAML:

```xml
<avc:CartesianChart xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:avc="https://github.com/slobber/av-charts"
                    Height="240">
    <avc:CartesianChart.HorizontalAxis>
        <avc:DateTimeAxis Title="Time" />
    </avc:CartesianChart.HorizontalAxis>
    <avc:CartesianChart.VerticalAxis>
        <avc:LinearAxis Title="Depth, m" />
    </avc:CartesianChart.VerticalAxis>
    <avc:LineSeries DisplayName="Depth"
                    ItemsSource="{Binding Readings}"
                    XPath="When"
                    YPath="Depth" />
</avc:CartesianChart>
```

About the markup:

- **The prefix**: the namespace is `https://github.com/slobber/av-charts` and the suggested prefix is `avc`,
  which is what the library asks a designer for.
- **Series are the content property**, so they sit under the chart.
- **All fourteen series have a non-generic twin** (`LineSeries`, `ColumnSeries`, `PieSeries`…), which is what
  a markup file needs: a generic argument cannot be written in XAML, so `XxxSeries<object>` is there to be
  used with **paths** instead of lambdas.
- **Path selectors**: besides `XPath`/`YPath`, each series has the ones it needs - `RadiusPath` (bubble),
  `OpenPath`/`HighPath`/`LowPath`/`ClosePath` (candles and high-low-close), `LowPath`/`HighPath` (ranges),
  `ErrorPath`, `MinimumPath`/`LowerQuartilePath`/`MedianPath`/`UpperQuartilePath`/`MaximumPath` (boxes) and
  `ValuePath`/`NamePath`/`LabelPath`/`ExplodePath` (pie). A path walks properties, fields and dictionary
  entries, indexes into a `List<T>`, and reads the value it lands on the way `ChartValue` reads anything
  (a number, an instant, a label).
- **Two halves of one example**: the picture and the code are equivalent, and every example in the gallery is
  written as a pair of files - the `.axaml` is the one to copy, the `.cs` builds the same chart by hand, and a
  test draws both and compares them pixel by pixel, so the two cannot drift apart quietly.

## 4. Put it on a page

Three rules of layout:

**Give it a height.** In a `StackPanel` or an unconstrained `ScrollViewer` a chart is measured against
infinity and lands on top of its neighbours. Use a `*` row, a docked fill, or an explicit `Height`.

**Let the axes keep their own room.** The plot is the control's bounds minus the gutter each axis asks for
(ticks, labels, title). Wider labels, longer titles and larger text all shrink the plot without you doing
arithmetic; shrinking the plot does not clip the labels. The outermost label sits centred on its tick and
reaches half a label past it, and that half is measured into the gutter too
(`AxisRenderer.MeasureOverhang`).

**Small means less furniture.** At 200×120 two labelled axes leave very little plot; that size wants the
labels off (`ShowLabels = false`), turned (`LabelRotation`), or spaced further apart (`MajorGridPitch`).

## 5. A chart and its series: several things on one plot

Any Cartesian series can go on **any** `CartesianChart`, which is why "a column chart" is not a kind of chart
class here but a `CartesianChart` with a `ColumnSeries` on it. It is also why a combination chart is nothing
special:

```csharp
chart.Series.Add(new ColumnSeries<Sale> { /* … */ });
chart.Series.Add(new LineSeries<Sale> { /* … */ });   // a line over the same columns
```

`Against Flex`: Flex has the chart type classes - `LineChart`, `ColumnChart`, `AreaChart`. They are not ported,
because `LineChart.as` is mostly a style pass over its series, and what `ColumnChart` really does (clustering
and stacking) is this library's **chart layout** (`CartesianChart.SeriesLayout`) - so it works on every chart
instead of on a class named after one of them.

A pie is the other branch: a `PieChart` with a `PieSeries<T>` on it. Several pies on one chart are drawn as
nested rings.

## 6. Getting it out

One chart can be laid out again at a size you ask for and written out as a bitmap:

```csharp
RenderTargetBitmap image = chart.RenderImage(new PixelSize(1200, 600));
image.Save("depth.png");

// Print wants more pixels and more detail, not a bigger blur:
using var stream = File.Create("depth-print.png");
chart.SaveImage(stream, new PixelSize(800, 400), scaling: 2);

// A browser's file handle only writes asynchronously:
await chart.SaveImageAsync(stream, new PixelSize(800, 400), scaling: 2);
```

One trap, said out loud: with `scaling` above one the bitmap's dots-per-inch is no longer 96, and Avalonia's
`Image` draws such a bitmap at its **pixel** size rather than the size you gave it (all three `Stretch` values
behave the same), so a preview shows one corner of the picture. A preview has to scale it itself - which is
what the gallery's Export page does.

## Where next

- Another kind of chart: [series and chart types](series.md).
- Where the data comes from, how missing values are drawn, and what to do with a hundred thousand points:
  [data](data.md).
- Why the horizontal axis is not in the order you expected: [axes, ticks and the window](axes.md).

---

[中文版](../../using/first-chart.md)
