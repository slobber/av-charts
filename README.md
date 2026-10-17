# AvCharts

A charting library for Avalonia 12 whose architecture and public type names follow Apache Flex's
`mx.charts`: `ChartBase` / `CartesianChart` / `Series` / `Axis` / `AxisRenderer` / `DataTransform` /
`ChartItem`, with the members named and shaped the C# way.

> **Status: v1 feature-complete, 0.1.1 published; v1.1 nearly done.** Everything the first version promised
> is implemented, rehearsed and shipped (see *What is in it* below); `AvCharts` and `AvCharts.Controls` 0.1.1
> are on NuGet.org, with the gallery attached to the release. The design is recorded in
> [`docs/design-brief.md`](https://git.cyber-stone.com/ipaas/av-charts/blob/master/docs/design-brief.md) -
> the decisions, what is built and what is left - with the measurements and the process behind them in
> [`docs/design-log.md`](https://git.cyber-stone.com/ipaas/av-charts/blob/master/docs/design-log.md).

## What is in it

- **Series**: line, area, step line, step area, plot (marks only), bubble, column, bar - any of them on any
  chart, which is how a column chart with a line over it works. Columns come clustered, overlaid or stacked.
- **Axes**: linear, category, date and time, duration and logarithmic, each with its own ladder of
  intervals; an axis of its own per series, which is what a second vertical scale is.
- **Data**: an `ItemsSource` with selectors, a filter, incremental updates when the source notifies, batching
  for readings pushed from another thread, and sampling (min/max, average, LTTB) for more readings than there
  are pixels. Missing readings are drawn as gaps, connected over, or at zero - never invented.
- **Drawing**: a small fixed set of layers, so hovering one point re-records one layer rather than the chart;
  a hundred thousand readings drawn in about thirty milliseconds.
- **Interaction**: crosshair that snaps to the nearest reading, data tips (one, or one per reading, stacked so
  they do not cover each other), selection with the keyboard semantics and the Ctrl/Shift of a file list, a
  legend that switches a series off, wheel zoom and drag panning, and per-record events with `ICommand`
  counterparts for view models.
- **Scrolling**: a `ChartScrollBar` along one side of a chart - thin, with no arrows on it - whose thumb is the
  share of the readings on screen and whose drag moves the window along them, and a `ChartOverview` that draws
  the whole recording as a shape with the window lying on it, to be dragged along or stretched by either end.
  The window is the same one the wheel and a drag on the plot move, so a chart panned by its bar is a chart
  panned.
- **Theme**: every colour is a token in a resource dictionary, with light and dark sets and a ten-colour
  palette; a host reskins a chart by overriding a token, and a control theme can be replaced outright.
- **Extension**: a custom series is four overrides; a custom axis brings its own ladder; a marker is an
  interface; anything that is not a series - a threshold, a band, an event marker - is a chart element; and a
  coordinate system of one's own is a transform handed to the chart.

## A first chart

```csharp
using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Series;
using AvCharts.Drawing;
using AvCharts.Data;
using AvCharts.Transforms;
using AvCharts.Elements;
using AvCharts.Controls;

var chart = new CartesianChart
{
    HorizontalAxis = new DateTimeAxis(),
    VerticalAxis = new LinearAxis(),
};

chart.Series.Add(new LineSeries<double>
{
    DisplayName = "Depth",
    ItemsSource = readings,          // whatever you have: a list, an array, a collection
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
});
```

Put the chart on a page and give the page the theme - one line, and every chart on it is dressed:

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

The gallery (`dotnet run --project src/AvCharts.Demo.Desktop`) has a page per feature, listed on the left, and one page
that is the whole acceptance list at once.

## Performance

Measured in Release with BenchmarkDotNet on a desktop machine - the numbers and how to reproduce them are in
[`docs/design-brief.md`](https://git.cyber-stone.com/ipaas/av-charts/blob/master/docs/design-brief.md) (§11). In short: a hundred thousand readings
drawn in 30ms, a frame of panning in 3.7-4.4ms once the chart is laid out
(the same path whether the window is moving or still - a MinMax series draws the same thinning every frame, so a drag frame and a still frame are pixel-identical), a hit test on them
in 70µs, ten thousand readings applied in a batch in 0.8ms, and a million readings held in 24MB. §11 also
carries the comparison against LiveCharts, under conditions matched on both sides.

## Layout

| Path | What it is |
|---|---|
| `src/AvCharts` | The charting model: axes, transforms, series, the drawing kernel. Avalonia's base only. |
| `src/AvCharts.Controls` | `ChartBase : Control`, data tips, legends, control themes and the colour tokens. |
| `src/AvCharts.Demo` | The gallery: a page per feature, and the pages the golden images are taken from. |
| `src/AvCharts.Demo.Desktop` | The gallery in a window. |
| `src/AvCharts.Demo.Browser` | The gallery in a page, for the published site. |
| `src/AvCharts.Tests` | Unit, contract, acceptance and golden-image tests. |
| `src/AvCharts.Benchmarks` | The performance table, measured rather than claimed. |
| `site/` | The landing and getting-started pages of the published site. |
| `docs/design-brief.md` | The decision record this implementation follows. |

## Building

```pwsh
dotnet build AvCharts.slnx -c Release
dotnet test src/AvCharts.Tests/AvCharts.Tests.csproj -c Release
dotnet run --project src/AvCharts.Demo.Desktop
dotnet run --project src/AvCharts.Demo.Browser    # the same gallery, in a browser
dotnet run -c Release --project src/AvCharts.Benchmarks -- --filter "*MathBenchmarks*"
dotnet tool restore; dotnet docfx docs/docfx.json      # the API site, into artifacts/docs
```

Building needs the WebAssembly workload once per machine - `dotnet workload install wasm-tools` - because the
gallery's browser head is part of the solution and is compiled ahead of time when it is published (a
`-p:RunAOTCompilation=false` publish leaves it interpreted and needs no workload).

The version comes from `version.json` and the git history (Nerdbank.GitVersioning); only a `v*` tag is a
public release, and the tag is what publishes them: `.github/workflows/release.yml` builds, tests, packs and
pushes both packages to NuGet.org - through a key minted for the run by trusted publishing rather than a stored
secret - and writes a GitHub release with the packages, their symbols and the Windows gallery. The internal
CyberStone feed stays in `nuget.config` for reading inside the org.

Two things about the build are deliberate. The public API is checked in: adding, removing or renaming a public
member fails the build until `PublicAPI.Unshipped.txt` says so, because the design brief freezes contracts and
that file is what holds them. And the golden images are only written when `AVCHARTS_UPDATE_GOLDENS=1`, so a
failing build can never quietly rewrite what it compares against - a changed picture is a decision someone
makes and looks at.

## Lineage

The API shape is derived from Apache Flex's `mx.charts` (Apache-2.0); this is an independent C#
implementation, not a port of its source. The default colour palette is inspired by SciChart.js's light
theme, independently tuned, with a separate dark set.

## Licence

MIT - see [LICENSE](LICENSE). The lineage above is credited rather than inherited: nothing here is copied from
Apache Flex's source, so its licence does not reach this one.

The gallery's Chinese is drawn in a subset of Noto Sans SC (SIL Open Font License 1.1), renamed to
`AvCharts Chinese` because the licence reserves the name its source was published under. The subset, the
licence it comes with (`src/AvCharts.Demo/Assets/NotoSansSC-OFL.txt`) and the reason a browser build needs it
at all are in `DemoFonts`.

## The site

The site has three parts, and `.github/workflows/pages.yml` publishes all of them on every push to `master`:

- <https://nandi.wang/av-charts/> - the landing page, and <https://nandi.wang/av-charts/get-started.html> -
  getting started, both hand-written in [`site/`](site/)
- <https://nandi.wang/av-charts/docs/> - the API reference, built by docfx out of the two libraries' XML
  comments
- <https://nandi.wang/av-charts/gallery/> - the gallery, compiled to WebAssembly and running in the browser
