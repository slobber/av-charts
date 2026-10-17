# Extending it

Extending this library is not an inheritance tree; it is a handful of interfaces that each do one job. Decide
which layer you are changing, then write the least that will do it:

| What you want to change | Where |
|---|---|
| the shape of a mark (another dot) | `IMarkerRenderer`: one method that adds a shape to a batch |
| what is drawn between the points | derive from `PlotSeries<T>` / `LineSeries<T>` and override `BuildInk` |
| a new way of reading and drawing | derive from `CartesianSeries<T>` and write the hooks |
| a new scale (its ladder, its labels) | derive from `AxisBase`, write the two mappings, and a ladder method if you need one |
| another coordinate system (polar and such) | derive from `DataTransform` and hand it to `chart.Transform` |
| something that is not a series (a threshold, a note, a carried control) | derive from `ChartElement` and add it to `chart.Elements` |
| a whole layer of drawing | derive from `ChartLayer` and add it with `AddLayer` |

## A custom series: the four hooks

The four hooks on `SeriesBase` are the contract, and the **order** is: `UpdateData` (read the source) →
`DescribeData` (say what the axes have to cover) → `BuildGeometry` (turn values into ink - once per change,
not once per frame) → `Draw` (put that ink on the screen). A test pins the order down: a series that keeps to
it can do anything, and one that does not will see an axis sized for data it no longer has.

In practice you rarely start at `SeriesBase`. Deriving from `CartesianSeries<T>` already gets you
`ItemsSource` and the selectors, `Filter`, incremental updates, `Push`, `MaxReadings`, sampling, the
per-axis mapping, the line a data tip writes, the `Items` view and hit testing along a segment - and the
default `UpdateData` and `DescribeData` along with them. **What is usually left is one thing**: drawing the
points your way.

A complete example - a stem plot: a vertical line from the axis' own zero to each reading, and a dot on it.

```csharp
public sealed class StemSeries<T> : CartesianSeries<T>
{
    private Geometry? _stems;

    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var zero = ZeroOn(ChartDimension.Y);     // the axis' own zero; NaN on a log axis, which has none
        var batch = new GeometryBatch();

        foreach (var (_, x, y, point) in PlacedRecords(context))   // a missing record is not in here
        {
            if (!double.IsNaN(zero) && context.TryUnitPoint(x, zero, out var foot))
            {
                batch.AddPolyline([foot, point]);                  // two points are one segment
            }

            batch.AddCircle(point, 2.5);
        }

        _stems = batch.Build();
    }

    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        if (_stems is not null && PaletteStroke is { } pen)
        {
            context.DrawGeometry(null, pen, _stems);
        }
    }
}
```

The rules that go with it:

- What `SeriesRenderContext` hands you is "where the plot is, how to turn values into positions, and how to
  line a stroke up with the pixel grid": `PlotBounds`, `Transform`, `RenderScaling`, `TryPoint(x, y, out
  point)`, `TryUnitPoint(x, y, out point)`, `Snap` / `SnapStroke`, `Bands`. A series never asks the chart how
  big it is - which is what keeps the same series code working in a control, in a headless test and in a
  renderer that is not Avalonia at all.
- **The geometry is cached in a field**: built once, drawn many times, rebuilt only when the mapping moved.
  That is what "a frame of panning does not rebuild geometry" rests on, and it is how the library's own series
  are written.
- Want your own hit shape? Override `HitTestItem` (the default measures the distance to a segment). Want your
  own view of the records? Override `Items`.
- Where the ink comes from: `PaletteStroke` / `PaletteFill` are what the chart handed this series in
  declaration order. **A series with colours of its own overrides `HasOwnColours`**, which is how it takes no
  slot in the palette.
- To take part in stacking, implement `IStackable`; to stand in a band (the column-and-bar family), implement
  `IColumn`.

## A custom marker

```csharp
public sealed class StarMarker : IMarkerRenderer
{
    public void AddTo(GeometryBatch batch, Point centre, double radius) =>
        batch.AddPolyline(StarPoints(centre, radius), isClosed: true);
}

line.Marker = new StarMarker();
```

The interface is one method, and what it hands you is a `GeometryBatch`: `AddPolyline`, `AddCurve`,
`AddRectangle`, `AddCircle`, `AddRing`, `AddDiamond`, `AddCross`, `AddTriangle`, `AddWedge`, and one
`Build()` at the end that turns the whole batch into a single piece of geometry. Drawing a whole batch at once
is far cheaper than a `Geometry` a point.

`Against Flex`: in Flex, reskinning a ChartItem is a graphical or programmatic itemRenderer hung on a series.
Here that splits in two - changing a mark's shape is the interface above (no new series type needed), and
changing how the line itself is drawn is a series subclass.

## A custom axis

The least an axis has to answer is two questions: how a value becomes a number in unit space (`TryToUnit`) and
how it comes back (`FromUnit`). The ladder and the labels have defaults; override them when they are wrong.

```csharp
/// A binary axis: unit space is log2(value), and the ticks land on 1, 2, 4, 8…
public sealed class BinaryAxis : AxisBase
{
    public override ChartValueKind ValueKind => ChartValueKind.Number;

    public override bool TryToUnit(ChartValue value, out double unit)
    {
        unit = 0;

        // Nought and negatives have no place on this axis: returning false is "no value on this
        // dimension", rather than moving them somewhere that looks acceptable - which is what a log
        // axis does too.
        return value.TryGetNumber(out var number) && number > 0
            && double.IsFinite(unit = Math.Log2(number));
    }

    public override ChartValue FromUnit(double unit) => Math.Pow(2, unit);

    /// One step is a whole octave - a power of two, in this space
    protected override double NiceInterval(double target) => Math.Max(1, Math.Round(target));

    /// The labels write the value, not the exponent it sits at
    public override string FormatValue(double unit) =>
        Math.Pow(2, unit).ToString("G4", CultureInfo.InvariantCulture);
}
```

Which override does which job:

| Override | When |
|---|---|
| `TryToUnit` / `FromUnit` | always (this is what makes it this axis) |
| `NiceInterval` | the lines are evenly spaced but have to land on another set of readable numbers (the default is 1 / 2 / 2.5 / 5 × 10ⁿ) |
| `ResolveStep` | the lines are **not** evenly spaced (a date axis stepping by months is one), returning an `AxisStep` of your own; `EvenStep` is the common one |
| `FormatValue(unit)` / `FormatValue(unit, step)` | the labels are written by your own rule; the step-taking one is for "one line a month writes the year and the month" |

An axis **holds settings and never results** (`Resolve` is a pure function), so a custom axis can be a shared
instance measuring several charts at once.

## Chart elements

The things on a chart that are not series - a threshold line, a reference band, an event mark, a note carrying
a button:

```csharp
public sealed class Threshold : ChartElement
{
    public double Value { get; set; } = 8;

    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        if (!renderContext.TryPoint(ChartValue.FromNumber(Value), ChartValue.FromNumber(Value), out var at))
        {
            return;
        }

        var plot = renderContext.PlotBounds;
        context.DrawLine(_pen, new Point(plot.Left, at.Y), new Point(plot.Right, at.Y));
    }
}

chart.Elements.Add(new Threshold { Value = 8 });
```

Four things:

- Elements are drawn **under the series** (they are in the `AnnotationLayer`), so a note never covers a
  reading; an `AxisSection` is the other way round and writes over the axis labels.
- An element **does not stretch the axes** by default (a threshold line usually belongs beyond the readings);
  override `DescribeData` and it will be counted.
- Work a position out in `Draw`, or keep it in a field and work it out again in `MappingChanged()` - that is
  the "the mapping moved" notification.
- `Invalidate()` when the element changes, and this layer redraws.

**Carrying a real control**: `Visual` hands over a `Layoutable` and `VisualAnchor` says which pixel its
**middle** stands on. A button beside a tip, a small card - that is how. It can be pressed, it can take the
keyboard, it can wear a host's own theme, none of which a painted shape can do. What it costs is a real
control in the visual tree, so it is a few of them, never one per reading. The gallery's Annotations page has
a complete one.

## Another coordinate system: a transform

`DataTransform` owns "value ↔ pixel". The library's other implementation is `PolarTransform`: the angle in
turns (from three o'clock, growing anticlockwise) and the radius as a fraction of the circle's own, centred on
the plot, as large as fits. A pie's wedges are placed by it, and the layers, the hit tests and the data tips go
on asking the questions they always ask - which is what makes another coordinate system this small a change.

To write one: override `TryValueToPoint`, `TryUnitToPoint`, `TryPointToValue` (the third is what hit tests and
tips need), and `UpdateRanges` / `GetDimensionLength` if you need them. Then:

```csharp
chart.Transform = new MyTransform();   // the layers, the axes, the series and the elements move across
```

It is an advanced entry: everything else in the library assumes values across and values up.

## A whole layer

For a layer of your own drawing (furniture of your own, a background of your own), derive from `ChartLayer`,
override `DrawLayer`, and add it with `AddLayer` (which is `protected`, so from a subclass of `ChartBase`).
The layer is handed its `PlotBounds` and `RenderScaling` by `Configure`, and it is its own invalidation
granularity: one layer repaints, not the chart.

---

[中文版](../../using/extension.md)
