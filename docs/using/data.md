# 数据

数据的形状是：**你的对象** + **读哪两个字段**。系列不复制你的对象、不要求你实现接口、不要求你转换类型；
它按选择器把每个记录读成一个 `ChartValue`，之后一切都在"单位空间"里算。

## 数据源

`ItemsSource` 收任何 `IEnumerable<T>`：`List<T>`、数组、`ObservableCollection<T>`、LINQ 的结果、
数据库分页读出来的东西 —— 图表需要时会重新枚举它。

```csharp
chart.Series.Add(new LineSeries<Reading>
{
    ItemsSource = readings,                 // 你自己的对象
    XSelector = reading => reading.When,     // 横着读哪个字段
    YSelector = reading => reading.Depth,    // 竖着读哪个字段
});
```

`Flex 对照`：Flex 的图表吃 `ArrayCollection`、XML、`HTTPService` 的结果，还要写 `xField`/`yField` 字符串。
这里没有"取数"那一层 —— 取数是宿主的事，`HttpClient` 也好、仓储也好，取到之后赋给 `ItemsSource`，
或者用下文的 `Push` 推给已经在跑的图表。XAML 里的写法见[第一个图表](first-chart.md)的 `XPath`/`YPath`。

同一个集合可以喂给几个系列，各读各的字段；也可以每张图持有自己的集合。

## 选择器

两个选择器都是 `Func<T, ChartValue>`。`ChartValue` 有隐式转换，所以 `double`、`double?`、`DateTime`、
`DateTimeOffset`、`DateTime?`、`string` 都能直接返回：

```csharp
XSelector = r => r.When,            // DateTimeOffset → 时刻
YSelector = r => r.Depth,           // double → 数值
```

它认识四种值（`ChartValueKind`），轴认哪一种见[轴那一页](axes.md)：

| 选择器返回 | 含义 |
|---|---|
| `double` | 数值 |
| `DateTime` / `DateTimeOffset` | 时刻 |
| `string` | 类别（给分类轴用） |
| `null` / `double.NaN` / 空串 | **这个维度没有值**（`ChartValue.Empty`），不是零 |

几条要知道的规则：

- **不给 `XSelector`**，横坐标就是这个记录在"保留下来的记录"里的位置（下标）。适合"每行一个样本"的序列。
- **不给 `YSelector`**，每个点在纵向上都是缺失的：画得出来的是横轴，不是数据。
- 选择器的返回值决定这个点**算不算一个值**。缺值和 0 是两个不同的东西，整张库都按这条办：值域不为缺值
  让位，提示会把缺值说成缺值。

## 过滤

`Filter` 是 `Func<T, bool>`，返回 `false` 的记录**不进系列**：不画、不进值域、不打得中，而且**不占槽位**
—— 分类轴上的位置、柱的槽位、横坐标的默认下标，都是按留下来的记录算的。

```csharp
Filter = reading => reading.Quality == Quality.Good,
```

这与空值（`ChartValue.Empty`）是两回事：被过滤掉的记录像从来没有过，空值占着自己的位置、只是没有值。
这两条规则在设计简报 §4 里写死了，因为它们决定了轴怎么摆、提示怎么说、堆叠算多少。

## 缺值怎么画：`NullValueMode`

```csharp
series.NullValueMode = NullValueMode.Gap;   // 默认：断开
```

| 取值 | 画法 | 什么时候要它 |
|---|---|---|
| `Gap`（默认） | 断口：线在这里结束，从下一个有效值重新开始 | 读数没来，就是没来 |
| `Connect` | 跳过这条记录，把两侧连起来 | 记录本身没有意义，不值得留一个洞 |
| `Zero` | 画在**轴自己给出的零点**上 | 计数器丢了一次滴答，实际是空转 |

它是**画法**，不是数据：三个模式下系列留着的值一样、轴覆盖的范围也一样，所以改它只重画，不重读源。
`Zero` 也**不会**让轴为 0 让地方 —— 缺值不是数据值，为它撑开的值域在描述不存在的数据。要看见 0，就设
`axis.Minimum = 0`。对数轴没有"零点"这回事，于是 `Zero` 在那里退化成 `Gap`。

堆叠里的缺值另有约定：**不画高度，但仍然是缺失**，所以提示分得清"这里是 0"和"这里没有数据"。

## 数据变了

四种情况，处理方式不同：

| 你做了什么 | 会发生什么 |
|---|---|
| 在 `ObservableCollection<T>` 末尾添加 | 追加一条，增量读数 |
| 原地替换集合里的某一项 | 只重读那一项（若它还在原位） |
| 在中间插入、删除，或 `Reset` | 整个源重读（横坐标会整体位移，增量无从谈起） |
| 记录自己实现了 `INotifyPropertyChanged` 并改了字段 | 重读源 |
| 改了一个普通 `List<T>` 的内容 | 什么都不会发生 —— 调 `series.InvalidateData()` |

换轴也会重读：单位空间变了（线性 → 对数，或换了分类轴），之前留住的值不再成立。

`InvalidateData()` 是公开的，就是给"我的源不会广播"这种情况准备的入口。

## 数据从别的线程来

读数常常不是你在 UI 线程上算出来的：串口、采样卡、后台任务。`Push` 可以在**任何线程**调用：

```csharp
// 采集线程
while (running)
{
    var reading = scope.Read();
    series.Push(reading);       // 只进缓冲，不碰绘制
    await Task.Delay(1);
}
```

它做三件事：

1. 把记录**加进 `ItemsSource`**（源和值不分家），并放进一个加锁的缓冲；
2. 抛 `Pushed` 事件 —— 在**推送的那个线程**上，只用来请求一次 UI 线程的 pass，处理器里不能画东西；
3. 图表每一帧排空一次缓冲（`ApplyPushed`），一批一百个点只触发一次失效。

连带的规矩：既然记录会进入源，**源本身要能加**（`ICollection<T>` 且不是只读的）。给它一个数组，`Push`
会当场抛错而不是把数据悄悄丢掉。`PendingPushes` 可以跨线程读，用来看排队有多深。

一秒一千个读数的画廊页（Streaming）就是这么跑的：**一帧一次重排**，不是一千次。

## 只留最近的 N 条

```csharp
series.MaxReadings = 5000;      // 默认 null：不设上限
```

有界之后系列只保留最新的 N 条，旧的从值里丢出去（源那边你自己管，或者用 `Push` + 一个有界集合）。
滑窗式的示波器、滚动日志就靠它把内存压在一个常数上。

## 采样：点比像素多的时候

```csharp
series.Sampling = SamplingMode.MinMax;
```

| 取值 | 做法 |
|---|---|
| `None`（默认） | 全都画，再密也画 |
| `MinMax` | 每个像素列保留最低和最高两个点，**首尾点永远保留** |
| `Average` | 每个像素列取平均，噪音最大的信号用它最耐看 |
| `Lttb` | 最大三角形法：挑出最能代表整条曲线形状的那些点 |

几条性质：

- 采样在**已经映射成像素坐标**的点上做纯算术，不碰轴、不碰系列、不碰样式 —— 所以它可以脱离图表单独测试。
- 桶数是绘图区的像素列数；**点数不超过列数时不生效**（原样返回同一个列表，不复制）。
- 分桶只在一个"连续段"内部进行，**缺值两侧永远不会被连出来**。
- 它只影响画出来的东西：你的数据、`Series.Units`、轴的值域都不变。所以命中测试、提示、概览页都是完整的。

`MinMax` 是实时曲线的正确选择：尖峰不会被平均掉，最新的一点不会因为"落在了最后一个桶里"而消失。

## 拿回记录

系列给你的是**视图**，不是副本：

```csharp
foreach (ChartItem item in series.Items)          // 屏幕上画着的（采样之后的）
{
    object? mine = item.Item;                     // 你自己的对象
    ChartValue y = item.Y;                        // 数据空间的值
    Point? where = item.Position;                 // 画在哪（没画就是 null）
}

ReadOnlySpan<double> all = series.Units(ChartDimension.Y);   // 全部读数，单位空间，缺值是 NaN
```

`ChartItem` 是一个"系列 + 下标"的轻量视图：值现读，不复制数据，所以一百万个读数走一遍不会造出一百万个对象。
`Items` 只给视野里的记录，`Units` 给全部 —— 概览页（把整段录音画成一个缩略形状）就是靠后者。

下一站：这些值被轴怎么解释、怎么摆到像素上，见[轴、刻度与视窗](axes.md)。

---

[English version](../en/using/data.md)
