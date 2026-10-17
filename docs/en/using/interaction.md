# Interaction

A reader does four things with a chart: points at it, moves the window somewhere else, picks a reading out, and
stretches a stretch of it. This page is what each of those is, what it says, and how to listen.

## Hit testing

```csharp
IReadOnlyList<HitData> hits = chart.FindDataPoints(point);   // one a series, nearest first
HitData? nearest = hits.Count > 0 ? hits[0] : null;
```

What comes back has everything on it: `Distance` (in logical pixels), `ChartItem` (which record), `Item` (your
own object), `X`/`Y` (the values, in data space), `Position` (where it is drawn), `DisplayText` (the line a tip
would write) and `SeriesColor` (the colour the tip and the legend swatch show beside it).

The entry points:

| Method | What it gives |
|---|---|
| `FindDataPoints(point)` | the nearest record of each series, ordered by distance |
| `FindDataPoints(point, sensitivity)` | the same, with a radius of your own |
| `GetAllDataPoints()` | every record drawn on screen (after sampling) |
| `GetItemsInRegion(rect)` | the records inside a rectangle |

`MouseSensitivity` is 5 logical pixels by default. **What can be seen is what can be hit**: a record with no
value is never a hit (there is nothing to point at), a point drawn by `Zero` can be, columns, bars and wedges
are hit by shape (a point inside one is at distance zero), and a line is hit by its segments.

## Data tips

```csharp
chart.DataTipMode = DataTipMode.Item;      // the default: beside the reading
chart.DataTipMode = DataTipMode.Mouse;     // following the pointer
chart.DataTipMode = DataTipMode.None;      // off
```

`Item` is the default - v1's promise is that data tips are on. Three things come with it:

- **`ShowAllDataTips = true`** puts a tip on every reading in view, **stacked so that they do not cover one
  another** (Flex's `TipStack`, and the same machinery here). It is meant for a chart with a handful of
  records on it: a series of a hundred thousand points would ask for a hundred thousand tips.
- **`DataTipFunction`** is a `Func<HitData, string>` that replaces the line. Without one, the text is what the
  axes wrote - each dimension through **its own** axis, so a tip on a second scale is in that scale's units.
  This is Flex's `dataTipFunction`.
- **`DataTip` is a templated control**: for another shape of box, write a `ControlTheme` for `DataTip` (a
  template reads its `Data` and `Text`). A tip is a real control in the chart's visual tree.

## The crosshair

```csharp
chart.ShowCrosshair = true;            // off by default
HitData? where = chart.CrosshairHit;   // the reading it is on
```

The crosshair **snaps to the nearest valid point on the whole chart** (it does not honour the 5px pointer
sensitivity - snapping is not hitting), and each of its two badges is written by the axis of **the series that
point belongs to**, so two vertical scales each get their own numbers. A badge is put against the axis when it
fits in the gutter, and moved inside the plot when it does not, so it is always whole and always on the chart.

## Selection

```csharp
chart.SelectionMode = ChartSelectionMode.Multiple;
int count = chart.SelectedItems.Count;      // the selected records (ChartItem views)
ChartItem? caret = chart.Caret;             // the record the caret is on
```

The default is `None`, because **selection is what makes a chart want the keyboard**: a chart that takes the
keyboard away from a host's text boxes without being asked is a chart in the way. Asking for selection is what
makes a chart focusable, and the focus ring is drawn for real. Turning selection off lets go of what was
selected.

The pointer semantics (a file list's, not an invention):

| Gesture | What happens |
|---|---|
| click a reading | it is selected (under `Single`, what was selected before is not) |
| `Ctrl` + click | toggles that one |
| `Shift` + click | takes the stretch from the anchor |
| click empty space | lets the selection go |
| `Alt` + drag | region select: the readings in the band are picked out |

The keyboard:

| Key | What happens |
|---|---|
| `←` `→` | the record before/after this one in the same series |
| `↑` `↓` | the series above/below |
| `Home` / `End` | either end of this series |
| `Space` | selects the record the caret is on |
| with `Ctrl` | **moves the caret only, leaving the selection alone** |
| with `Shift` | takes the stretch from the anchor |

Neither end wraps around. `SelectionChanged` fires **only when the selection actually changed**: clicking the
same record again says nothing, and so does a `Ctrl` move of the caret.

## Events and commands

```csharp
chart.ItemClick += (_, e) => details.Text = $"{e.Item}";
chart.SelectionChanged += (_, e) => status.Text = $"{e.Selection.Count} selected";
chart.ItemDoubleClickCommand = openCommand;    // an ICommand: run on a double click, with the ChartItemEventArgs
```

| Event | When |
|---|---|
| `ItemPointerEntered` / `Exited` / `Moved` | the pointer arrives in, leaves, or moves inside a record |
| `ItemPointerPressed` / `Released` | pressed and released on a record |
| `ItemClick` / `ItemDoubleClick` | one click, two |
| `SelectionChanged` | the selection really changed |

The arguments are `ChartItemEventArgs`: `Hit` (everything the hit test found), `ChartItem`, `Item` (your own
object) and `Selection` (the chart's own live list, **not a copy** - copy it yourself if you need to keep it as
it was). View models get three commands - `ItemClickCommand`, `ItemDoubleClickCommand`,
`SelectionChangedCommand`; the six pointer-level events are for views and have no command forms.

Three things to know:

- **A data point is not a thing on screen**: there is no `Visual` behind it and nothing to subscribe to. Every
  per-record event is made up by the chart out of a hit test, and its `sender` is the chart.
- **A drag is not a click**: `Pressed` and `Released` are reported, `Click` is not.
- **A double click is the platform's `ClickCount`**, the chart does not keep its own timer.

## Scroll bars and the overview

The window is not only reachable by gesture; two controls can be attached to a chart:

```csharp
var under = new ChartScrollBar { Chart = chart };
var beside = new ChartScrollBar { Chart = chart, Orientation = Orientation.Vertical };
var whole = new ChartOverview { Chart = chart };
```

| Control | What it does | Knobs |
|---|---|---|
| `ChartScrollBar` | its track is drawn over the plot's own stretch; the thumb's length is the share of the readings on screen and its position is where the window is among them. Drag the thumb to pan, press the track to move the window to the pointer. No arrow buttons at either end, and 8px thick by default | `Chart`, `Orientation`, `MinThumbLength` (16 by default), read-only `CanScroll` |
| `ChartOverview` | draws **the whole recording** as a shape (one line a series, broken at a gap), with the window lying on it: drag the middle to pan, either end to stretch, anywhere else to jump there | `Chart`, `Orientation`, `WindowFill`, `WindowPen`, read-only `CanScroll` |

Both push the same window, so a chart panned by its bar and a chart zoomed by the wheel are one chart. The
gallery's Scrolling page puts them round a chart in a `Grid` - copy that.

## The entries a touch surface wants

The pointer, wheel and keyboard handlers are thin shells: the rules live on protected methods, and a host
driving a touch panel, a pen or a remote input takes the same road - `WheelZoom(notches, at, modifiers)`,
`DragStart` / `DragMove` / `DragEnd`, `SelectAt(at, modifiers)`, `BandStart` / `BandMove` / `BandEnd`,
`TrackPointer(at)`, `PressAt` / `ReleaseAt`, `MoveCrosshair` / `HideCrosshair`, `ShowDataTipsAt` /
`HideDataTips`, `Navigate(key, modifiers)`.

---

[中文版](../../using/interaction.md)
