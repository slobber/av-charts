# Looks and theme

Every colour a chart draws is a **token** in a resource dictionary, and every control's template lives in a
`ControlTheme`. This page is how to bring those in, change one, or replace the lot - plus the things that are
not colour: the palette, the legend, the elements on a chart, and getting a picture out.

## Where the theme is, and the three ways in

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

```csharp
// or, where a host builds its resources in code:
Application.Current!.Resources.MergedDictionaries.Add(ChartTheme.Create());

// or once, for every chart built afterwards:
ChartDefaults.Configure(defaults => defaults.Theme = ChartTheme.Create());
```

How the three relate: `ChartDefaults.Current.Theme` is the **last** thing consulted (a chart's own resource
scope is read first), so a theme put on one window is the theme that window gets. `ChartDefaults` is a startup
call, and a chart already on screen keeps what it was dressed with.

## The tokens

The keys are public constants on `AvCharts.Themes.ChartTokens`, grouped by what they are for:

| Group | Tokens |
|---|---|
| Surface | `SurfaceBackground`, `SurfaceBorder`, `SurfaceFocusRing` |
| Axes | `AxisLine`, `AxisTick`, `AxisTickMinor`, `AxisGridLineMajor`, `AxisGridLineMinor`, `AxisGridLineOrigin`, `AxisLabelForeground`, `AxisTitleForeground`, `AxisBandFill`, `AxisBandFillAlt` |
| Marks | `MarkerFill`, `MarkerStroke`, `MarkerHoverFill`, `MarkerHoverStroke`, `MarkerSelectedFill`, `MarkerSelectedStroke` |
| Crosshair | `CrosshairLine`, `CrosshairLabelBackground`, `CrosshairLabelForeground` |
| Data tips | `DataTipBackground`, `DataTipBorder`, `DataTipForeground`, `DataTipShadow` |
| Legend | `LegendBackground`, `LegendBorder`, `LegendForeground`, `LegendItemDisabled` |
| Scroll bar and overview | `ScrollbarTrack`, `ScrollbarThumb`, `ScrollbarThumbHover`, `OverviewBackground`, `OverviewWindowFill`, `OverviewWindowStroke` |
| Financial | `FinancialUp`, `FinancialDown` |
| Rubber band | `SelectionRubberBandFill`, `SelectionRubberBandStroke` |

The dictionary carries a **light and a dark set** (`ThemeDictionaries`), and a chart follows the variant of
whatever it is put into. A dark chart on a light page is not a second theme, it is a `ThemeVariantScope`.

**Changing one token** is a value under the same key:

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
    <SolidColorBrush x:Key="AxisGridLineMajor" Color="#1A000000" Opacity="0.35" />
</Application.Resources>
```

**Replacing the look** is your own `ControlTheme` under the same type key (`ChartBase`, `DataTip`, `Legend`,
`LegendItem`). That is where "what this control looks like and what its defaults are" lives: the library's own
one wires tokens onto `ChartBase` (background, border, focus ring, axis line, ticks, grid, minor grid, label
brush, title brush, crosshair, the hover and selected inks, the rubber band). Its `Background` is more than
decoration: a control with nothing to render is transparent to hit testing, and `Transparent` is a brush - that
is what makes a chart receive pointer events at all.

`Against Flex`: Flex dresses charts with CSS class selectors and style properties on an `AxisRenderer`. There
is no CSS here - tokens, XAML styles and `ControlTheme` are the same job in this shape, and "a style applies
where it is put in the resource tree" is easier to reason about than a global cascade.

## The palette: who gets which colour

The ten colours are in the theme, in order, and the chart hands them out in the order the **series are
declared**:

```csharp
chart.Series.Add(line);        // slot 1
chart.Series.Add(area);        // slot 2
chart.Series.Add(column);      // slot 3
```

- **A series with colours of its own takes no slot**: if `area.Fill` is set in the third line above, the
  column still takes slot 2. (`Against Flex`: Flex's rule is that setting a colour on the first series shifts
  every series after it, which is a rule that makes one edit and breaks four.)
- More series than slots **cycles**.
- Per-point colours have a way out: `chart.PaletteStroke(index)` / `chart.PaletteFill(index)`, and a pie takes
  the palette per record on its own account.
- A series' `Color` is what it is actually drawn in, so a legend's swatch and a tip's swatch always agree with
  the line.

## The ink of each series

| Series | What draws in what |
|---|---|
| `LineSeries<T>` | `Stroke`, `StrokeThickness` |
| `AreaSeries<T>` | `Fill` (the region), `Stroke` (the line) |
| `PlotSeries<T>` | `MarkerFill`, `MarkerStroke`, `Marker`, `MarkerSize` |
| `ColumnSeries<T>` / `BarSeries<T>` | `Fill`, `Stroke` |
| `RangeSeries<T>` | `Fill`, `Stroke` |
| `BoxSeries<T>` | `Fill`, `Stroke` (the median, the whiskers and the caps read its brush) |
| `ErrorSeries<T>` | `Fill` (the mark), `Stroke` (its three hairlines read the brush only) |
| `CandlestickSeries<T>` / `HighLowCloseSeries<T>` | `RiseFill`/`RiseStroke`, `FallFill`/`FallStroke`, falling back to the theme's two financial colours |
| `PieSeries<T>` | the palette, per record; `LabelBrush`, `LabelFontSize`, `LabelTypeface` dress the labels |

One detail worth saying out loud: a series that draws hairlines (an error bar, a candle's wick) reads **only
the brush** of the pen it is given, never its thickness - a tick marks a level rather than covering it.

## The legend

```csharp
var legend = new Legend { Chart = chart, Placement = LegendPlacement.Right };
```

`Legend` is an `ItemsControl`: it lists the series of the chart it is pointed at, one `LegendItem` a line (a
templated control with a swatch, a name, and whether that series is switched off). Clicking a line turns that
series' `Visible` over:

```csharp
chart.Series[0].Visible = false;    // what clicking a legend row does
```

A hidden series takes part in **nothing**: it is not drawn, it does not grow the axes, it is not stacked and
stands in no band, an axis it brought is taken away, and no hit test or tip can find it. What it keeps is its
place in the chart's list of series, which is what makes showing it again a matter of one property.

The four placements (`Top`/`Bottom`/`Left`/`Right`, `Right` by default) decide which way the lines run. A legend
is not inside the plot: put it in a cell beside the chart in your own `Grid`/`DockPanel`.

## Elements on a chart

Everything that is not a series lives here: thresholds, reference bands, event marks, a host's own things.

```csharp
chart.Series.Add(readings);

chart.Elements.Add(new Section                 // a stretch of the plot
{
    Dimension = ChartDimension.Y,
    From = 3,
    To = 6,
    Name = "target",
    Fill = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4), 0.12),
});

chart.HorizontalRenderer.AddSection(new AxisSection   // a stretch of the axis
{
    From = 4,
    To = 6,
    Name = "rollout",
});
```

Two rules:

- **Elements are drawn under the series** (they live in the `AnnotationLayer`, and the layer order is fixed),
  so a named band never covers a reading. A section along the axis is the other way round: it writes over the
  labels inside it.
- Either end of a `Section` may be left unset, and the unset end runs to the edge of the plot; an `AxisSection`
  is the same, with the end of the axis. A `Name` writes a line inside it, and the tick labels it covers stand
  aside.

A threshold line and an event mark are each a dozen lines of a custom element; the gallery's Annotations page
has both in full (a dashed threshold, a vertical line with a line of text beside it), and
[extending it](extension.md) shows the shape.

**An element can also carry a real control**: `ChartElement.Visual` hands over a `Layoutable`, `VisualAnchor`
says which pixel its middle stands on, and the chart puts it there. A button beside a tip, a small card
anchored to a reading - that is the way. The catch is that each one is a real control in the chart's visual
tree, so it is a few of them, never one per reading.

## Getting a picture out

```csharp
chart.SaveImage(File.Create("depth.png"), new PixelSize(1200, 600));
chart.SaveImage(stream, size, scaling: 2);        // print: twice the pixels and twice the detail
await chart.SaveImageAsync(stream, size);          // a browser's file handle only writes asynchronously
```

It lays the chart out again at the size you ask for rather than enlarging what is on screen: the axes, the
labels and the sampling are all recomputed for the new size. With `scaling` above one the bitmap's
dots-per-inch is no longer 96 and Avalonia's `Image` draws it at its pixel size - a preview has to scale it
itself (the gallery's Export page has the code). More in [a first chart](first-chart.md).

## Breathing room

A chart keeps no padding of its own: the plot is the control's bounds minus the gutter the axes ask for. For
space around a chart, give its container a margin; to make an axis take less room, move its furniture
(`LabelMargin`, `TickLength`, `FontSize`, `ShowLabels`, `LabelRotation`) - all of which ask for a layout pass.

---

[中文版](../../using/formatting.md)
