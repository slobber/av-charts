# Performance

This page has a narrow purpose: the numbers, the conditions they were measured under, and which knob to turn
when something feels slow.

## The numbers

A hundred thousand readings, a 680×320 plot, a desktop machine, BenchmarkDotNet (`dotnet run -c Release
--project src/AvCharts.Benchmarks`; the whole table and its conditions are §11 of the
[design brief](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md)):

| What | Measured |
|---|---|
| first frame of a hundred thousand points (sampling on) | about 30ms |
| a frame of panning (window moving, thinning kept) | **3.7ms**, 651KB |
| a frame of zooming (a bucket width crossed each frame) | 4.1ms |
| a frame with the window moving and a feed running | 4.4ms |
| one hit test on those hundred thousand points | about 70µs |
| ten thousand readings applied in a batch (`ApplyPushed`) | about 0.8ms |
| a million readings kept (what the library holds) | about 24MB, which is 24 bytes a reading; CI's guard is under 50MB |

**Only ratios within one session are readable.** BenchmarkDotNet switches the machine to the high-performance
power plan, and the same work drifts by ±30% between sessions - and by more on another machine. Compare
against another number from the same session rather than treating this table as a promise.

One detail that is easy to misread: **a frame of a drag and a still frame are the same path** - a `MinMax`
series draws the same thinning every frame (panning changes the window, not the bucket width), so the two are
pixel-identical (`DragThinningTests`). There is no "it only goes blurry while you move it".

## Where a frame's money goes

A relayout is a rebuild of geometry (about 600KB of allocation for a hundred thousand points), and drawing is
just painting geometry that already exists. Today **panning rebuilds it too** (about 4MB a frame). Caching the
min/max envelope in the data space and dragging that instead has been prototyped and measured (10.3 → 4.7ms a
frame, 4.15 → 0.21MB, at the cost of the picture no longer being pixel-identical) but is not in the library; it
is one of the outstanding items in §14 of the
[design brief](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md). By the same token,
switching light/dark currently rebuilds geometry (about 4.2MB), which does not match the "and does not rebuild"
that §11 asks for - also recorded in §14.

## Which knob to turn

In the order worth trying:

1. **Turn sampling on.** `series.Sampling = SamplingMode.MinMax` (or `Average` / `Lttb`). It does not engage
   when the points fit the pixel columns, so a small series pays nothing.
2. **Bound the window.** `series.MaxReadings = 5000` keeps the newest so many and makes memory a constant.
3. **Batch the pushes.** Readings from another thread want `Push` and the chart's once-a-frame drain, not an
   `InvalidateData` per reading; a batch of a hundred costs one invalidation.
4. **Let an invalidation stay in its layer.** Hovering one point re-records one layer; do not write an element
   of your own that asks for the whole chart to be redrawn.
5. **A fixed scale and an even grid** (the ECG and oscilloscope shape): `AxisScaleMode.Fixed` with
   `PixelsPerUnit` keeps one unit of data the same width, so scrolling does not re-thrash the grid.
6. **Keep the data side simple.** Every point is read once: a lambda that reads a property is fastest, and a
   path selector (`XPath`/`YPath`) goes through reflection - it is there for markup files.

## Threads

There is one rule and it does not bend: **a series' values are written on the thread that draws it**. So:

- readings fetched elsewhere go in with `Push(record)` (a locked buffer behind it; nothing is drawn);
- the chart drains them once a frame (`ApplyPushed`);
- `Pushed` is raised on **the pushing thread**, and its only job is to ask for one pass on the UI thread -
  nothing may be painted in the handler;
- `PendingPushes` may be read from any thread, and is how you see how deep the queue is.

A pushed record goes into `ItemsSource`, so the source has to be **one that can be added to**
(`ICollection<T>`, not read-only). Hand it an array and `Push` throws on the spot - which is better than losing
data quietly, and much better than finding out a few hundred readings later.

## Memory

Twenty-four bytes a reading: **two `double`s in unit space** (16 bytes, two arrays) and **one reference to your
own object** (8 bytes - because data tips and events hand the record back to you, and the `Item` on a hit is
your object). So the cost is about 24 bytes a reading plus the records themselves. A test in CI pins it down (a
million readings under 50MB, with the caller's own 8MB of readings subtracted). `ChartItem` is a lazy view, so
walking a million readings does not build a million objects.

---

[中文版](../../using/performance.md)
