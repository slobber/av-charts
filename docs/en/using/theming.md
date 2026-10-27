# Theme and palette

An AvCharts theme has two layers: **resource tokens** decide the surface, axes, text, tips and series colours;
**`ControlTheme`** decides which templates and default properties a control uses. Most reskinning needs only
tokens. It does not require a new control or a change to the code that supplies the data.

This page covers bringing the theme in, how Light and Dark work, how the palette assigns colours, how to define
your own palette, and how one series can override its theme colour.

## Bring in the default theme

Merge the default resource dictionary into an application, window or page:

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

Where a host builds its resources in code, merge a fresh copy of the default theme:

```csharp
Application.Current!.Resources.MergedDictionaries.Add(ChartTheme.Create());
```

Or set the default once before charts are created:

```csharp
ChartDefaults.Configure(defaults => defaults.Theme = ChartTheme.Create());
```

Tokens are looked up in this order: **the chart's resources → resources on logical ancestors →
`Application.Resources` → `ChartDefaults.Current.Theme`**. A window can therefore override the application
theme, and a single chart can change only its own colours.

The theme also supplies a hit-testing prerequisite. A control with nothing to render is transparent to pointer
input in Avalonia, so the theme's `Surface.Background` gives a chart a visible or transparent surface. Without a
theme, a chart usually has neither its expected colours nor working hover, click and wheel input.

## Tokens: public colour names in the theme

Token keys are defined on `AvCharts.Themes.ChartTokens`. The names are public API. Code can use the constants and
the `SeriesStroke` / `SeriesFill` helpers instead of spelling strings.

| Purpose | Main tokens |
|---|---|
| Chart surface | `SurfaceBackground`, `SurfaceBorder`, `SurfaceFocusRing` |
| Axes and grid | `AxisLine`, `AxisTick`, `AxisGridLineMajor`, `AxisGridLineMinor`, `AxisLabelForeground`, `AxisTitleForeground` |
| Series palette | `SeriesStroke(1..10)`, `SeriesFill(1..10)` |
| Marks and interaction | `MarkerHoverFill`, `MarkerHoverStroke`, `MarkerSelectedFill`, `MarkerSelectedStroke` |
| Crosshair and tips | `CrosshairLine`, `CrosshairLabelBackground`, `DataTipBackground`, `DataTipBorder`, `DataTipForeground` |
| Legend and overview | `LegendBackground`, `LegendBorder`, `LegendForeground`, `OverviewWindowFill` |
| Financial and selection | `FinancialUp`, `FinancialDown`, `SelectionRubberBandFill`, `SelectionRubberBandStroke` |

A token has one of two value types, and the two are not interchangeable:

- **A fill/brush** is an `IBrush`, such as a `SolidColorBrush`. Surfaces, text, regions and the inside of marks
  use it.
- **A stroke** is a `Pen`. It carries a brush plus width, dashes and other stroke settings. Axis lines, grid
  lines and the palette's `Series.Stroke1..10` use it.

For example, this changes the major grid line only in the resource scope where it is defined:

```xml
<SolidColorBrush x:Key="Axis.GridLine.Major"
                 Color="#1A000000" />
```

## Light, Dark and `ThemeVariantScope`

The default theme supplies Light and Dark token sets in `ThemeDictionaries`. A chart follows the
`ThemeVariant` of the scope it is in:

```xml
<ThemeVariantScope RequestedThemeVariant="Dark">
    <avc:CartesianChart Height="240">
        <!-- series and axes -->
    </avc:CartesianChart>
</ThemeVariantScope>
```

In code:

```csharp
var darkChart = new ThemeVariantScope
{
    RequestedThemeVariant = ThemeVariant.Dark,
    Child = chart,
};
```

Changing `ThemeVariantScope.RequestedThemeVariant` makes an existing chart read the tokens and repaint again; it
does not require rebuilding the chart. This supports a dark chart on a light page, and also changing the theme of
a whole window at run time.

To provide colours for both variants, define them in the same dictionary's `ThemeDictionaries`:

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
    </ResourceDictionary.MergedDictionaries>

    <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light">
            <Pen x:Key="Series.Stroke1" Brush="#1473E6" Thickness="1" />
            <SolidColorBrush x:Key="Series.Fill1" Color="#771473E6" />
        </ResourceDictionary>
        <ResourceDictionary x:Key="Dark">
            <Pen x:Key="Series.Stroke1" Brush="#79B8FF" Thickness="1" />
            <SolidColorBrush x:Key="Series.Fill1" Color="#7779B8FF" />
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

The example leaves out the other slots. Keys that are not overridden continue to come from the default theme.

## How the palette assigns colours

The palette has ten pairs. Pair `n` consists of:

```text
Series.Stroke{n}   // Pen, for lines and outlines
Series.Fill{n}     // IBrush, for regions, columns and the inside of marks
```

The chart assigns slots in **series declaration order**:

```csharp
chart.Series.Add(depth);    // slot 1: Series.Stroke1 / Series.Fill1
chart.Series.Add(speed);    // slot 2: Series.Stroke2 / Series.Fill2
chart.Series.Add(flow);     // slot 3: Series.Stroke3 / Series.Fill3
```

Three rules follow:

1. **A series with a colour of its own takes no slot.** If `speed.Stroke` or `speed.Fill` is set explicitly,
   `flow` still takes slot 2. Recolouring one series does not shift the colours of every series after it.
2. **Slots cycle.** The eleventh series that has no colour of its own starts again at colour pair 1.
3. **Stroke and fill are paired by slot.** They can be designed separately, but a pair should normally be the
   solid and tinted versions of the same hue.

The chart exposes per-record lookups. The index is zero-based, and negative values or values above ten also cycle:

```csharp
var outline = chart.PaletteStroke(0);   // Pen
var fill = chart.PaletteFill(0);        // IBrush
```

The legend swatch and data-tip colour read `series.Color`. It returns the colour the series is actually drawn in:
the series' own colour when it has one, and the chart's assigned palette colour otherwise.

## Define your own palette

The direct way is to override `Series.Stroke1..10` and `Series.Fill1..10` in resources the chart can see. This
builds a complete ten-colour palette from a list and applies it to one chart:

```csharp
string[] inks =
[
    "#1473E6", "#D14A8A", "#19A974", "#E58B2E", "#7657C8",
    "#3C9BD6", "#A5A72A", "#D65C5C", "#8B6F47", "#607D8B",
];

var palette = new ResourceDictionary();

for (var index = 0; index < inks.Length; index++)
{
    var color = Color.Parse(inks[index]);
    var slot = index + 1;

    palette[ChartTokens.SeriesStroke(slot)] = new Pen(new SolidColorBrush(color), 1);
    palette[ChartTokens.SeriesFill(slot)] = new SolidColorBrush(color, 0.47);
}

var chart = new CartesianChart
{
    Resources = palette,
    // axes and size...
};
```

This changes only the colours. The other axis, text and surface tokens still come from the theme above the chart.

Put the same overrides in `Application.Resources` when the whole application should share a palette. Put them on a
window or chart when that scope needs a different palette. The default size is `ChartTokens.PaletteSize`, which is
currently ten.

### Choosing a custom palette

- Check that adjacent slots have enough contrast on both light and dark surfaces.
- Do not rely on colour alone. Legend text, marker shapes, line styles and direct labels are secondary identity
  channels.
- `Series.Stroke` is a `Pen`, but `LineSeries` draws with its own `StrokeThickness`. Set line width on the series;
  the palette pen's `Thickness` is not a global line-width setting.
- Keep each stroke/fill pair in the same hue. The fill should usually be a noticeably more transparent version of
  the stroke.

## Colours on a series

An explicit series colour has the highest priority and is not overwritten by the chart palette:

```csharp
var depth = new LineSeries<Reading>
{
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
    Stroke = Brushes.SteelBlue,
};

var flow = new ColumnSeries<Reading>
{
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Flow,
    Fill = new SolidColorBrush(Color.Parse("#77D14A8A")),
    Stroke = new Pen(Brush.Parse("#D14A8A"), 1),
};
```

The colour entry points differ by series:

| Series | Theme or explicit colour entry point |
|---|---|
| `LineSeries<T>` | `Stroke`, `StrokeThickness` |
| `AreaSeries<T>` | `Fill`, `Stroke` |
| `PlotSeries<T>` | `MarkerFill`, `MarkerStroke` |
| `ColumnSeries<T>` / `BarSeries<T>` | `Fill`, `Stroke` |
| `RangeSeries<T>` / `BoxSeries<T>` | `Fill`, `Stroke` |
| `ErrorSeries<T>` | `Fill`, `Stroke` |
| `PieSeries<T>` | the palette per record by default; `LabelBrush` for its labels |
| `CandlestickSeries<T>` / `HighLowCloseSeries<T>` | `RiseFill`, `RiseStroke`, `FallFill`, `FallStroke` |
| `HeatSeries<T>` | `ColorScale`, where colour represents a third value |

There are a few exceptions worth remembering:

- **A pie takes the palette per record.** Its first wedge starts at the pie's slot in the chart's series list, and
  it cycles when there are more wedges than palette slots.
- **Rising and falling colours are not palette slots.** Unless set explicitly, candles and price bars use
  `FinancialUp` / `FinancialDown`, because rising and falling are properties of the reading rather than the
  series' position in the list.
- **A heat map uses `HeatColorScale`.** This maps a continuous value to colours and does not use the normal
  series palette.
- **Setting only line width does not count as a series colour.** `StrokeThickness` changes the shape, but the
  series still takes its colour from the palette.

When colour comes from business state rather than series identity, prefer a series property or a display hook such
as `CellStyleSelector`. When colour is the identity of the whole series, prefer the palette so the legend, data
tip and drawing stay in agreement automatically.

## Change colours or change control templates

Changing tokens is enough for different colours, widths and resource values. Provide your own `ControlTheme` under
the same type key only when the layout or structure must differ: `ChartBase`, `DataTip`, `Legend` or
`LegendItem`.

A complete reskin does not require subclassing the chart controls. Merge the default theme and your resource
dictionary into the host's resources. Start with a small override and replace a whole `ControlTheme` only when the
extra structure is necessary; existing series, interaction and tests then keep working.

---

[中文版](../../using/theming.md)
