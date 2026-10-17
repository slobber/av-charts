# Data

The shape of it is: **your objects** plus **which two fields to read**. A series does not copy your objects, ask
them to implement an interface, or make you convert anything; it reads each record through the selectors into a
`ChartValue`, and everything after that happens in unit space.

## Where the data comes from

`ItemsSource` takes any `IEnumerable<T>`: a `List<T>`, an array, an `ObservableCollection<T>`, the result of a
LINQ query, a page at a time out of a database. The chart enumerates it again when it needs to.

```csharp
chart.Series.Add(new LineSeries<Reading>
{
    ItemsSource = readings,                 // your own objects
    XSelector = reading => reading.When,     // what to read across
    YSelector = reading => reading.Depth,    // what to read up
});
```

`Against Flex`: Flex's charts eat an `ArrayCollection`, XML, or the result of an `HTTPService`, and are told
which fields to read as strings. There is no fetching layer here - fetching is the host's business, whether
that is an `HttpClient` or a repository - and what it fetches is assigned to `ItemsSource`, or pushed into a
chart that is already running (see below). The XAML form of it is on [a first chart](first-chart.md).

One collection can feed several series, each reading its own fields; or each chart can hold its own.

## Selectors

Both selectors are `Func<T, ChartValue>`, and `ChartValue` has implicit conversions, so a `double`, `double?`,
`DateTime`, `DateTimeOffset`, `DateTime?` or `string` can be returned directly:

```csharp
XSelector = r => r.When,            // DateTimeOffset → an instant
YSelector = r => r.Depth,           // double → a number
```

It understands four kinds of value (`ChartValueKind`); which axis takes which is on the
[axes page](axes.md):

| The selector returns | What it is |
|---|---|
| `double` | a number |
| `DateTime` / `DateTimeOffset` | an instant |
| `string` | a category, for a category axis |
| `null` / `double.NaN` / an empty string | **no value on this dimension** (`ChartValue.Empty`), which is not zero |

Rules worth knowing:

- **No `XSelector`**, and the horizontal value is the record's position among **the records that were kept**
  (its index). That is what a "one sample a row" series wants.
- **No `YSelector`**, and every point is missing vertically: what draws is the horizontal axis, not data.
- What a selector returns decides whether a point **is a value at all**. A missing value and a nought are two
  different things and the whole library is built on that: a range never makes room for a missing value, and a
  tip says a missing reading is missing.

## Filtering

`Filter` is a `Func<T, bool>`, and a record it refuses **does not enter the series**: it is not drawn, it does
not grow the range, nothing can hit it, and it **leaves no slot behind** - a category's place, a column's slot
and the default horizontal index are all counted over the records that were kept.

```csharp
Filter = reading => reading.Quality == Quality.Good,
```

This is a different thing from a missing value (`ChartValue.Empty`): a filtered record is as if it had never
existed, while a missing value keeps its place and simply has no reading. Both rules are frozen in §4 of the
design brief, because they decide where the axis sits, what a tip says and how much a stack adds up to.

## How a missing value is drawn: `NullValueMode`

```csharp
series.NullValueMode = NullValueMode.Gap;   // the default: a break
```

| Value | The drawing | When you want it |
|---|---|---|
| `Gap` (the default) | a break: the line ends here and starts again at the next valid reading | the reading never arrived, so it never arrived |
| `Connect` | the record is skipped and its neighbours are joined | the record itself means nothing and is not worth a hole |
| `Zero` | drawn where **the axis puts zero** | a counter that missed a tick was idle, not broken |

This is a decision about **drawing**, not about data: the values the series kept are the same in all three
modes, and so is what the axes cover, so switching modes repaints without reading anything again. `Zero` does
**not** ask the axis to make room for zero either - a missing value is not a data value, and a range that grew
to include it would be describing data that is not there. To see zero, set `axis.Minimum = 0`. A log axis has
no zero to be drawn at, so `Zero` degrades to `Gap` there.

Stacking has its own rule for a missing value: **no height is drawn, but it is still missing**, so a tip can
tell "this is nought" from "there is no reading here".

## When the data changes

Four cases, four different things happening:

| What you did | What happens |
|---|---|
| appended to an `ObservableCollection<T>` | one record is appended, read incrementally |
| replaced an item in place | that item is re-read (if it is still where it was) |
| inserted or removed in the middle, or reset the collection | the whole source is read again (the horizontal values all shift, so there is nothing incremental about it) |
| changed a property, with the record implementing `INotifyPropertyChanged` | the source is read again |
| edited a plain `List<T>` | nothing at all - call `series.InvalidateData()` |

Changing the axis re-reads as well: the unit space changed (linear to logarithmic, or a category axis in
place of a linear one), so the values that were kept no longer mean what they meant.

`InvalidateData()` is public for exactly the "my source does not announce itself" case.

## Data arriving from another thread

Readings usually are not computed on the UI thread: a serial port, a sampling card, a worker. `Push` may be
called from **any** thread:

```csharp
// on the acquisition thread
while (running)
{
    var reading = scope.Read();
    series.Push(reading);       // into a buffer; nothing is drawn here
    await Task.Delay(1);
}
```

It does three things:

1. the record is **added to `ItemsSource`** (the source and the values do not live apart) and put in a locked
   buffer;
2. `Pushed` is raised - on **the pushing thread**, whose only purpose is to ask for one pass on the drawing
   thread; nothing may be painted from the handler;
3. the chart drains the buffer once a frame (`ApplyPushed`), so a batch of a hundred readings costs one
   invalidation.

The rule that comes with it: because pushed records go into the source, **the source has to be one that can be
added to** (`ICollection<T>`, and not read-only). Hand it an array and `Push` throws where the mistake is made,
rather than losing the record quietly. `PendingPushes` may be read from any thread, which is how you see how
deep the queue is.

The gallery's Streaming page is a thousand readings a second driven this way: **one layout a frame**, not a
thousand.

## Keeping only the newest readings

```csharp
series.MaxReadings = 5000;      // null by default: no limit
```

Bounded, a series keeps the newest so many records and drops the oldest out of its values (the source is yours
to manage, or use `Push` with a collection that bounds itself). This is what keeps a scrolling oscilloscope or
a rolling log at a constant cost in memory.

## Sampling: more points than pixels

```csharp
series.Sampling = SamplingMode.MinMax;
```

| Value | What it does |
|---|---|
| `None` (the default) | draws everything, however dense |
| `MinMax` | keeps the lowest and the highest point of every pixel column, and **always the first and last points** |
| `Average` | one point a column, at the average of the points in it - the calmest picture of a noisy signal |
| `Lttb` | largest-triangle-three-buckets: picks the points that best carry the shape of the whole curve |

Things that are true of all of them:

- Sampling is pure arithmetic on points that are **already mapped to pixels**. It touches no axis, no series and
  no style, which is why it can be tested without a chart at all.
- The bucket count is the number of pixel columns in the plot, and **it does not engage when the points fit**:
  the same list comes back, uncopied.
- Bucketing happens inside one run of points, so **a gap is never bridged** by the thinning.
- It affects what is drawn and nothing else: your data, `Series.Units` and what the axes cover are untouched.
  Hit testing, tips and the overview therefore see everything.

`MinMax` is the right choice for a live trace: a peak is not averaged away, and the newest reading cannot
disappear for having landed in the last bucket.

## Getting the records back

What a series hands you is a **view**, not a copy:

```csharp
foreach (ChartItem item in series.Items)          // what is on screen, after sampling
{
    object? mine = item.Item;                     // your own object
    ChartValue y = item.Y;                        // the value, in data space
    Point? where = item.Position;                 // where it is drawn, or null
}

ReadOnlySpan<double> all = series.Units(ChartDimension.Y);   // everything, in unit space, NaN where missing
```

`ChartItem` is a lightweight view of "a series and an index": the values are read when asked and nothing is
copied, so walking a million readings makes a million of nothing. `Items` is what is in view, `Units` is the
whole of it - which is what draws a recording at another size, where a picture of all of it cannot be made out
of the part that happens to be in view.

Next: how the axes read those values and put them on the screen - [axes, ticks and the window](axes.md).

---

[中文版](../../using/data.md)
