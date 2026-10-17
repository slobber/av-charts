# 轴、刻度与视窗

轴做两件事：把数据值折成**单位空间**里的一串单调数（`TryToUnit`），以及决定这一串数里哪一段落在屏幕上
（`Resolve`）。前者决定点画在哪，后者决定看见多少。这一页把五种轴、刻度与标签的装饰、以及窗口的手势讲完。

## 五种轴

| 轴 | 认什么值 | 单位空间是什么 | 梯级 |
|---|---|---|---|
| `LinearAxis` | 数值 | 值本身 | 1 / 2 / 2.5 / 5 × 10ⁿ |
| `CategoryAxis` | 类别（字符串） | 槽位号 | 整数个槽位 |
| `DateTimeAxis` | 时刻 | 1970 年起的毫秒 | 钟表步长 → 日 → 月 → 年 |
| `TimeSpanAxis` | 数值（毫秒） | 毫秒数 | 与时刻轴同一套钟表规则 |
| `LogAxis` | 数值 | 十进制指数（100 → 2、0.1 → −1） | 10 的幂；间隔取 1/2/5 个数量级 |

```csharp
chart.HorizontalAxis = new DateTimeAxis { Title = "Time" };
chart.VerticalAxis = new LinearAxis { Title = "Depth, m" };
```

**价值种类要对得上。** 如果值的种类不是这根轴认识的（时刻喂给线性轴、字符串喂给对数轴），`TryToUnit`
返回 `false`，这个点在那个维度上就按**缺失**处理 —— 不画，也不会被挪到某个位置上去。所以"图是空白的"
最常见的两个原因之一，就是把轴和值配错了（另一个是没接主题、没给高度，见
[常见问题](troubleshooting.md)）。

**为什么要"单位空间"。** 叠加、采样、命中测试、选择、缩放全部只在这个空间里算，它们因此对轴一无所知：
换一根轴，整套东西照样成立。这也是"横坐标是保留下来的记录的位置"这条默认选择器能工作的原因。

## 范围：Fit 与 Fixed

```csharp
axis.ScaleMode = AxisScaleMode.Fit;      // 默认：按数据铺满
axis.Minimum = 0;                        // 想让 0 在视野里，就写它
axis.Maximum = 100;
```

| 模式 | 范围从哪来 | zoom/pan 改动什么 |
|---|---|---|
| `Fit`（默认） | 数据说了算；`Minimum`/`Maximum` 设了就优先 | 两端都写，窗口随手势走 |
| `Fixed` | `PixelsPerUnit` 说了算：一个单位数据占多少像素恒定 | zoom 改 `PixelsPerUnit`，pan 改 `Minimum` |

`Fixed` 是"真实尺度"的读法：更宽的图显示**更多数据**，而不是把同样的数据拉得更开。心电图纸、交易图
要的是这个。它还有两条具体语义：

- 网格**锚在整格上**（刻度落在 `Interval` 的整数倍上），末格可以是残缺的，但网格一直铺到绘图区边缘。
- 在 `Fixed` 下 `Maximum` 被忽略（窗口的末端由尺度决定），`axis.IsMaximumIgnored` 为 `true` —— 好让宿主
  告诉用户，而不是让他纳闷。

`Flex 对照`：Flex 的轴绑定数据，没有"固定像素尺度"这一档（那边要这么做只能靠改 `minimum`/`maximum`
跟着数据跑）。分档 zoom（吸附到预设档位，而不是任意缩放）还没做，见[设计简报](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md) §14。

## 间距与刻度落在哪

```csharp
axis.Interval = 10;              // 不给就自己选
axis.MajorGridPitch = 60;        // 默认 60px：自动选间距时瞄准的像素间隔
axis.MinorGridPitch = 12;        // 默认 12px：细网格瞄准的间隔（五格一个粗格）
axis.Separators = [0, 25, 50, 75, 100];   // 显式指定刻度位置
```

- **`Interval` 不设时，间距是自动选的**：按可用像素瞄准 `MajorGridPitch`，然后**吸附到可读的数**
  （1/2/2.5/5 × 10ⁿ）。所以格距会在 60px 上下呼吸，最多两倍；2.5 这一级就是为这个加的，没有它 2 和 5 之间
  会跳一个 2.5 倍。
- **每根轴有自己的梯级**：时刻轴从 10µs 起按钟表步长（秒/分/时/日）往上走，超过一周按整日步进到两周，
  然后交给日历的 1/2/3/6 个月，再往上按整年（1/2/5/10…，每十年一档）；分类轴只在整槽上落刻度；对数轴
  只在 10 的幂上落。
- **`Separators`** 是"这个领域的刻度就是这几个值"时的出口：给它一组单位空间里的位置，替代自动生成的刻度。
  注意**换一个新列表才会通知**，原地改列表不会。

## 轴的装饰

轴的设置分两处：**轴自己**（覆盖什么、怎么数）在 `IAxis` 上；**怎么画**在 `AxisRenderer` 上。笛卡尔图
用两个属性把渲染器递给你：

```csharp
var x = chart.HorizontalRenderer;      // chart.HorizontalAxis 是那根轴本身
x.Placement = AxisPlacement.Start;     // 近边（下/左）；End 是远边（上/右）
x.TickPlacement = AxisTickPlacement.Outside;
x.TickLength = 4;
x.MinorTickLength = 2;
x.MinorTickPlacement = AxisTickPlacement.Outside;
x.ShowLabels = true;
x.LabelRotation = 45;                  // 度；标签太长时的第一招
x.StaggerLabels = true;                // 第二招：错成两行
x.FontSize = 11;
x.LabelGap = 4;                        // 刻度到标签的空气
x.LabelMargin = 4;                     // 标签到控件边缘的空气
x.TitleGap = 4;                        // 标签到标题的空气
x.MinimumGutter = 56;                  // 至少要留这么宽，不管标签需要多少（默认 0）
```

| `AxisTickPlacement` | 画在哪 |
|---|---|
| `None` | 不画刻度，轴就剩一条线和标签 |
| `Outside`（默认） | 朝绘图区外，占 gutter |
| `Inside` | 朝绘图区里，不占地方 |
| `Cross` | 骑在轴线上，一半里一半外 |

**改任何一个都会重新布局**，不只是重画：标签藏起来会把 gutter 还给绘图区，标题出现会要一块地方。这是
`AxisRenderer.Changed` 的意义 —— 轴的这些设置和轴自己的一样，都会改变绘图区的大小。

`MinimumGutter` 是给"几张图共用一根轴"准备的：房间本来是按**各自的标签**量出来的，于是同一个时刻在不同的
图上落在不同的列上。把各自让出的宽度取最大、按差值设给每一根轴，绘图区就对齐了 —— 两行算术，画廊的 Panes
页有完整的写法。

## 标题、名字与分段

```csharp
axis.Title = "Depth, m";        // 画在图上，轴在说自己量什么
axis.DisplayName = "Depth";     // 图例或列表里叫它什么；不画在图上
```

标题在竖轴上默认自下而上读（`TitleDirection = AxisTitleDirection.BottomToTop`），另一档是 `TopToBottom`。

**轴分段**用来标出"这一段是有名字的、有意义的"：

```csharp
chart.HorizontalRenderer.AddSection(new AxisSection
{
    From = startOfNight,
    To = endOfNight,
    Fill = new SolidColorBrush(Color.Parse("#20808080")),
    Name = "Night",
});
```

分段有名字时，覆盖到的刻度标签会让位（不跟名字打架）；`From`/`To` 不给一端，那一端就延伸到轴的末端。

## 标签文字怎么来的

刻度上的字由轴的 `FormatValue(unit)` 写，有的轴还看**步长**（`FormatValue(unit, step)`）：时刻轴上一格
是一个月时，标签写 `2026-11` 而不是 `2026-11-01`；一格是一年时写 `2026`。默认实现忽略步长，所以自己写的
轴不受影响。

现成的三种读法：

- `LinearAxis` 用 `G9` 写数值。这不是随手定的：默认的 `G` 会把 0.2 累加三次写成 `0.6000000000000001`，
  而 gutter 是按最宽的标签量的 —— 一个标签能吃掉 200px 宽的图的五分之一。
- `DateTimeAxis` 按上面说的"步长决定精度"写；`DisplayOffset` 决定标签用哪个时区的钟面（网格和标签也落在
  那个日历上）。
- `TimeSpanAxis` 写 `01:02:03.456`，跨天写 `3.01:02:03`；没给 `Interval` 时按数值自身的精度决定小数位。

要改格式，就派生一根轴覆盖 `FormatValue`（见[扩展](extension.md)）—— 这是唯一诚实的位置，因为 gutter
是按格式化之后的文字量的。

## 第二把尺

系列可以带自己的轴（`series.VerticalAxis = new LinearAxis { … }`），图表会把它画在**远边**、关掉它的网格，
每个维度最多画一根。三条规则和例子在[系列与图表类型](series.md#组合图与第二根纵轴)，两张图共用一根轴的
写法在[同一页的后面](series.md#一个轴实例服务好几张图)。

一根轴实例可以同时服务好几张图：轴只持有设置，从不持有结果。

## 窗口：缩放与平移

默认两个手势都开着：滚轮缩放（一个刻度 ×1.2，**锚在指针处** —— 指针下的那个读数不动），左键拖拽平移
（走够 3px 才算拖拽）。`Shift` 只动横轴，`Ctrl` 只动纵轴。嵌在滚动页面里的图表通常把滚轮关掉，
否则第一个刻度被图表吃掉：

```csharp
chart.ZoomOnWheel = false;
chart.PanOnDrag = false;
```

代码里也可以用同一套：

```csharp
chart.ZoomBy(1.2, anchor);          // 两轴同倍
chart.ZoomBy(1.2, 1.0, anchor);     // 只横轴
chart.PanBy(dx, dy);                // 逻辑像素；正数 = 内容被拖向右/下

Point? at = chart.DataToPoint(x, y);                 // 数据 → 控件坐标
(ChartValue X, ChartValue Y)? values = chart.PointToData(point);   // 反过来
```

窗口变化会报出来，宿主可以跟着更新别的东西（读数、状态栏）：

```csharp
chart.VisibleRangeChanged += (_, e) =>
    status.Text = $"{e.Dimension} {e.Range.Minimum:G4} – {e.Range.Maximum:G4}";
```

范围用的是**单位空间**，所以两张图可以这样联动起来 —— 谁也不必知道对方在量什么：

```csharp
first.VisibleRangeChanged += (_, e) =>
{
    if (e.Dimension != ChartDimension.X)
    {
        return;
    }

    IAxis axis = second.HorizontalAxis!;
    axis.Minimum = e.Range.Minimum;
    axis.Maximum = e.Range.Maximum;      // Fit 模式下两端都由你说了算
};
```

（`Fixed` 模式下 `Maximum` 是被忽略的，那边的联动得走 `PanBy`/`ZoomBy`。）

`DataToPoint`/`PointToData` 是 Flex 的 `dataToLocal`/`localToData` 的 C# 化；图表上做标记、自己画东西时用它们。

---

[English version](../en/using/axes.md)
