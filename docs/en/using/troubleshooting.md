# Troubleshooting

By symptom. The "why" matters more than the "what to do" in each of these, because most of them come back in
another shape.

## The chart is blank

Four causes, in the order they happen:

1. **No theme.** Every colour a chart draws is a token, and without the theme there are no colours.
   ```xml
   <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
   ```
2. **No height.** A chart takes its height from its container; inside one that does not constrain it (a
   `StackPanel`, a `ScrollViewer`) it is measured against infinity and gets clipped, squashed, or pushes its
   neighbours away. Give it a `Height`, or a `*` row.
3. **The axis and the values are of different kinds.** A `DateTimeOffset` on a `LinearAxis`, or a string on a
   numeric axis: `TryToUnit` returns `false`, those points are **missing** on that dimension, and missing
   points are not drawn. That is by design - the library will not guess what you meant.
4. **The selector reads the wrong thing.** A real one: the gallery's ECG page once squeezed a whole trace into
   a single column, and the cause turned out to be its `XSelector` being handed the record instead of its
   index - the values were drawn all right, just not against the right horizontal values. **Check what the
   horizontal selector reads first.**

## Clicking, wheeling and hovering do nothing

The chart has **nothing to render**. In Avalonia a control with no renderable content is transparent to hit
testing, and the event falls through to the window's `ContentPresenter`. What the theme gives a chart - a
`Background` from the `SurfaceBackground` token, even one painted `Transparent` - is what fixes it; a host with
a `ControlTheme` of its own that forgot the background will see exactly this.

## Events arrive that make no sense, or none arrive at all

A data point is not a thing on screen - there is no `Visual` behind it - so a per-record event is made up by
the chart out of a hit test, with the chart as the `sender`. Two things that are easy to misjudge: a drag is
not a click (`Pressed`/`Released` are reported, `Click` is not), and a double click is decided by the
platform's `ClickCount`, not by a timer of the chart's. `MouseSensitivity` (5 logical pixels by default)
decides how close the pointer has to be to be "on" a reading.

## Labels are clipped, or the plot is tiny

- The outermost label sits centred on its tick and reaches half a label outside it - and that half is measured
  into the gutter (`AxisRenderer.MeasureOverhang`), so nothing should be clipped by the chart. If it still is,
  look for a container that clips (`ClipToBounds`, a fixed height, a negative margin).
- A plot that is too small: `TickLength`, `LabelGap`, `LabelMargin` and `FontSize` all eat into it. Long labels
  want `LabelRotation`, or `StaggerLabels` for two rows; when there is really no room, `ShowLabels = false`,
  or space the ticks further apart (`MajorGridPitch`, larger, is fewer ticks and fewer labels).
- **Changing any of those asks for a layout pass**, not just a repaint: hiding the labels hands their room back
  to the plot (that is what `AxisRenderer.Changed` is for).

## A label reads 0.6000000000000001

That was the default `G` format on a linear axis: three accumulations of 0.2 come out like that, and a gutter is
measured from the widest label, so one such label could eat a fifth of a 200px chart. It writes `G9` now. An
axis of your own that overrides `FormatValue` has to look after this itself.

## Some readings are not drawn at all

In this order:

1. **The value is missing** (`null`, `NaN`, an empty string): that is "the reading never arrived", not zero.
   Look at `NullValueMode` - `Gap` breaks, `Connect` joins across, `Zero` draws it where the axis puts zero.
2. **`Filter` dropped it**: a dropped record **leaves no slot**, so the horizontal values and the column slots
   all close up around it.
3. **The axis does not understand the kind of value** (see the first section).
4. **`MaxReadings` dropped it**: a bounded window only keeps the newest so many.

## Columns are squashed, absurdly wide, or the first one is cut in half

- The band - how wide a slot is - is derived from the readings' own spacing, and `ColumnWidthRatio` (0.65 by
  default) and `MaxColumnWidth` decide how much of it a column takes.
- **Only band series take part in the layout**: a line takes no slot, so the columns in "columns with a line
  over them" stand in the whole of theirs (`SeriesLayout.Overlay`). Two groups of columns side by side want
  `SeriesLayout.Cluster`.
- The first and last columns are not cut in half: a band series declares half a slot of margin per record, and
  a test pins that down. If what you are seeing is something else (two series wearing each other's colours,
  say), check `Visible` and the palette slots.
- A category axis assigns **slots in the order categories are first seen**; if the data's order changed and the
  slots did not, call `ClearCategories()` and start again.

## The second scale is missing

- **One extra axis a dimension is drawn.** A third scale is not invented a place: the series measured against
  it are placed by it, they simply have no labelled axis of their own.
- An extra axis is drawn on the **far edge** (right or above) and carries **no grid** - both on purpose.
- If the series that brought it is hidden (`Visible = false`), its axis goes with it.

## The data changed and the chart did not

| What you did | What to do |
|---|---|
| edited a plain `List<T>` | `series.InvalidateData()` |
| inserted or removed in the middle, or cleared it | it re-reads the lot (as long as the collection announces itself) |
| replaced an element without the collection announcing it | `InvalidateData()` |
| changed a property on a record | the record implements `INotifyPropertyChanged`, or `InvalidateData()` |
| collected data on another thread | `Push` (see [performance](performance.md#threads)) |

## `Push` throws

The message is that a series can only be pushed to when `ItemsSource` is a collection that can be added to. The
reason is in [data](data.md#data-arriving-from-another-thread): a pushed record **enters `ItemsSource`** when
its batch is applied, so the source has to take it. An array is an `ICollection<T>` that refuses additions,
which is why `Push` says so at the point of the mistake rather than dropping the record.

## An exported picture is blurry, or shows one corner

`RenderImage` / `SaveImage` lay the chart out again at the size you give, rather than enlarging what is on
screen. So:

- more detail means a larger `PixelSize`, or a `scaling` (both affect detail; `scaling` also decides the
  bitmap's dots-per-inch);
- one corner of the picture means the bits' dots-per-inch is not 96 and Avalonia's `Image` is drawing it at
  its pixel size (all three `Stretch` values do this). A preview has to scale it itself, and the gallery's
  Export page has the code.

## The theme changed and the chart on screen did not

- A chart follows the variant of **the resource scope it is in**: for a dark chart on a light page, that is a
  `ThemeVariantScope`.
- `ChartDefaults.Configure` is a **startup** call: a chart already on screen keeps what it was dressed with
  until its own resources change - the same rule as any other theme.
- A token override has to be somewhere the chart can find it (the outermost application resources, a window's
  resources, or `ChartDefaults.Current.Theme`).

## A golden-image test fails

A golden image (a page rendered to a bitmap and compared with a checked-in PNG) is only written back when
`AVCHARTS_UPDATE_GOLDENS=1`, so a failing build can never quietly rewrite what it compares against - changing a
picture is a decision somebody makes and looks at. Record them, then look at every one of them.

## What Flex has and this does not

| Flex | Here |
|---|---|
| effects and animation (`SeriesSlide`, `SeriesZoom`, `SeriesInterpolate`, `showDataEffect`) | v1 does no animation; what is promised is one re-recording of the affected layers a frame. Animation means the host changing data or the window a frame at a time |
| data labels on a series | only a pie has labels of its own (`LabelSelector`); other series' values are read from data tips, or drawn by a chart element |
| CSS class selectors | tokens, XAML styles and `ControlTheme` (see [looks and theme](formatting.md)) |
| `PolarChart` and polar axes | `PolarTransform` with `PieChart` / `PieSeries<T>` |
| dragging and dropping a ChartItem | nothing; use `ItemPointerPressed` / `Released` and a host's own drag and drop |
| `minField` (a floating column) | stacking (`IStackable`), or draw that stretch as a `RangeSeries<T>` |
| a renderer class per item | a mark is the `IMarkerRenderer` interface; how a series draws is a subclass of it |

## Going further

This library's habit is to argue from evidence: every conclusion has a source in the
[design brief](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md) (the decisions and the
contracts) and the [design log](https://github.com/slobber/av-charts/blob/master/docs/design-log.md) (how it
was measured, how many times it changed, how many tests). Where a document and the code disagree, read the API
comments first - every public member has one, and they say why - and then those two.

---

[中文版](../../using/troubleshooting.md)
