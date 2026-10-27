# Using AvCharts

This is the guide to **using** the library: turning a set of readings into a chart, and then making that chart
say what you meant. It does not explain what each member does - that is the
[API reference](../../reference/index.md) - it
explains which lines to write to get the picture you are after, and why they are the lines.

The ground it covers follows the charts chapter of Apache Flex's *Using Flex* (an introduction to charts, the
chart types, displaying data and labels, formatting, events and effects), rewritten around the shape this
library actually has. Where the two differ, the difference is called out with `Against Flex`.

## Thirty seconds in

Two packages. `AvCharts.Controls` is the control half, and it depends on `AvCharts` - the model half: axes,
transforms, series and the drawing kernel.

```pwsh
dotnet add package AvCharts.Controls
```

One line in a page's resources dresses every chart on it:

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

Then draw a line. The data is **your own objects**, and selectors say which two fields to read:

```csharp
var chart = new CartesianChart
{
    Height = 240,
    HorizontalAxis = new DateTimeAxis(),
    VerticalAxis = new LinearAxis(),
};

chart.Series.Add(new LineSeries<Reading>
{
    DisplayName = "Depth",
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
});
```

The same chart in XAML:

```xml
<avc:CartesianChart xmlns:avc="https://github.com/slobber/av-charts" Height="240">
    <avc:CartesianChart.HorizontalAxis>
        <avc:DateTimeAxis />
    </avc:CartesianChart.HorizontalAxis>
    <avc:CartesianChart.VerticalAxis>
        <avc:LinearAxis />
    </avc:CartesianChart.VerticalAxis>
    <avc:LineSeries DisplayName="Depth" ItemsSource="{Binding Readings}" XPath="When" YPath="Depth" />
</avc:CartesianChart>
```

The series are the chart's **content** (`CartesianChart.Series` is its content property), so they are written as
children of the chart rather than under a property element.

## Four things to know before anything else

**Give a chart a height.** A chart takes its height from its container. Inside a `StackPanel`, or any
`ScrollViewer` that does not constrain height, it is measured against infinity, spills out of its cell and
lands on its neighbours. Put it in a `*` row of a `Grid`, in the fill of a `DockPanel`, or write
`Height="240"`.

**Bring the theme.** Every colour a chart draws is a token in a resource dictionary, so without the theme
there are no colours; and in Avalonia **a control with nothing to render is transparent to hit testing** - a
chart needs a `Background` (even `Transparent`) to receive pointer events, which is exactly what the theme's
`SurfaceBackground` supplies. A chart without the theme has no colours, and clicking or wheeling it does
nothing.

**A series is a model, not a control.** A series holds no `Visual`; the layer it is registered with does the
painting. That is why ten thousand points do not cost ten thousand controls, and why a data point has no event
to subscribe to - the per-record events (`ItemClick`, `ItemPointerEntered` and the rest) are made up by the
chart itself out of a hit test.

**Everything happens in unit space.** Each axis turns its dimension's values into a single monotonic number
(a linear axis: the value; a date axis: milliseconds; a log axis: the decimal exponent; a category axis: the
slot; a duration axis: milliseconds), and stacking, sampling, hit testing and selection are all arithmetic in
that space. It is what makes "swap the axis and the rest still works" true, and it is why a wrong selector
rarely looks wrong - the values are still drawn, just not where they belong.

## What happens in one pass

Understand these five steps and most "why is it not updating" questions answer themselves.

```
ItemsSource ─①UpdateData─▶ unit space double[] ─②DescribeData─▶ what each axis covers
                                    │
                                    └─③BuildGeometry─▶ geometry (pixels, rebuilt on change)
                                                              │
                    layout: axes measure their gutters, the plot is what is left ─④Draw─▶ a layer paints it
                                                              │
                    ⑤ hit testing / tips / crosshair / selection ◀┘
```

Four rules follow from it:

- **Data is re-read only when it changed** (`InvalidateData` is how you say so); **geometry is rebuilt only
  when the mapping moved**; **a change of ink only repaints** - the series' own comments are explicit about
  that last difference, because a line's shape is the same shape in a different pen.
- **The range belongs to the axis, not to you.** An axis is `AxisScaleMode.Fit` by default and fills the plot
  with what `DescribeData` declared; give it a `Minimum`/`Maximum` and it is your call instead.
- **Sampling happens before geometry and only affects what is drawn**: not your data, and not the range the
  axes cover.
- **One invalidation per layer**: hovering a single point re-records one layer rather than the chart. That is
  what keeps a million readings interactive.

## The ten pages of this guide, and where Flex says the same thing

| This page | What it covers | In *Using Flex* |
|---|---|---|
| [A first chart](first-chart.md) | installing it, dressing it, putting it on a page, XAML and code, export | Introduction to charts · Creating charts in ActionScript |
| [Series and chart types](series.md) | the fourteen series, combinations, a second axis, bands and stacking | Chart types · Using multiple data series · Using multiple axes |
| [Data](data.md) | sources, selectors, missing values, incremental updates, pushes, sampling | Defining chart data · Structure of chart data · Changing chart data at run time |
| [Axes, ticks and the window](axes.md) | the five axes, Fit and Fixed, ticks and labels, zoom and pan | Working with axes · Axis labels · Setting ranges · Positioning axes |
| [Theme and palette](theming.md) | tokens, Light/Dark, custom palettes and theme colours on series | Styles · Tick marks and grid lines · Strokes and fills |
| [Legends, elements and export](formatting.md) | legends, markers, elements on a chart, export and spacing | Formatting charts · Legends · Export |
| [Interaction](interaction.md) | hit testing, data tips, crosshair, selection, events, scrolling | Events and effects · Using the HitData object · Selecting chart items |
| [Extending it](extension.md) | custom series, axes, markers, elements and transforms | Creating custom renderers · A custom AxisRenderer |
| [Performance](performance.md) | the measured numbers, what a frame costs, which knob to turn | - (nothing in Flex) |
| [Troubleshooting](troubleshooting.md) | symptom → cause → what to do | - |

## Where it stands

v1 is complete and published (`AvCharts` and `AvCharts.Controls` 0.2.5 are on NuGet.org), and what is left in
v1.1 is listed in §14 of the [design brief](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md).
This guide describes what **works today**; what does not yet (the animation skeleton, stepped zoom) is said so
where it comes up.

To see it first: the gallery (`dotnet run --project src/AvCharts.Demo.Desktop`, or
[in a browser](https://nandi.wang/av-charts/gallery/)) has a page for every part of this guide. Every example
on those pages is shown three ways - the picture, the code and the axaml - and the axaml is the file that is
running, so what you copy is what you were looking at.

---

[中文版](../../using/index.md)
