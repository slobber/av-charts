# Legends, elements and export

Colour themes, resource tokens, Light/Dark and palettes now have their own page:
[Theme and palette](theming.md). This page covers what is not colour: legends, elements on a chart, export, and
space around the control.

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

The legend swatch reads the series' `Color`, so an explicit series colour, its palette slot and the tip swatch
stay in agreement. See [Theme and palette](theming.md#colours-on-a-series) for configuring the palette or the
colour of one series.

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
