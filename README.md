# AvCharts

A charting library for Avalonia 12 whose architecture and public type names follow Apache Flex's
`mx.charts`: `ChartBase` / `CartesianChart` / `Series` / `Axis` / `AxisRenderer` / `DataTransform` /
`ChartItem`, with the members named and shaped the C# way.

> **Status: pre-alpha.** The design is settled and recorded in [`docs/design-brief.md`](docs/design-brief.md);
> the code is being built against it.

## Layout

| Path | What it is |
|---|---|
| `src/AvCharts` | The charting model: axes, transforms, series, the drawing kernel. Avalonia's base only. |
| `src/AvCharts.Controls` | `ChartBase : Control`, data tips, legends, control themes and the colour tokens. |
| `src/AvCharts.Demo` | The gallery: every feature, light and dark side by side. Also the source of the golden images. |
| `src/AvCharts.Tests` | Unit, contract and golden-image tests. |
| `docs/design-brief.md` | The decision record this implementation follows. |

## Building

```pwsh
dotnet build AvCharts.slnx -c Release
dotnet test src/AvCharts.Tests/AvCharts.Tests.csproj -c Release
dotnet run --project src/AvCharts.Demo
```

The version comes from `version.json` and the git history (Nerdbank.GitVersioning); only a `v*` tag is a
public release. Packages go to the internal CyberStone feed configured in `nuget.config`.

## Lineage

The API shape is derived from Apache Flex's `mx.charts` (Apache-2.0); this is an independent C#
implementation, not a port of its source. The default colour palette is inspired by SciChart.js's light
theme, independently tuned, with a separate dark set.
