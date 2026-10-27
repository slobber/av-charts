# 系列与图表类型

一张图 = 一个 `ChartBase` + 若干个系列。系列决定"这些读数画成什么样"，图表决定"坐标怎么铺、轴画在哪"。
这张页面上有：每个系列画什么、要什么选择器、有哪几个旋钮，以及怎么把几样东西叠在一张图上。

## 一张表

| 系列 | 画什么 | 主要旋钮 | 画廊里看 |
|---|---|---|---|
| `LineSeries<T>` | 折线 | `Stroke`、`StrokeThickness`、`Curve`、`Marker` 一族 | Lines |
| `AreaSeries<T>` | 折线 + 线下的区域 | `Fill`（区域）、`Stroke`（线） | Lines |
| `StepLineSeries<T>` | 阶梯线：先横后竖 | 同折线 | Lines |
| `StepAreaSeries<T>` | 阶梯面积 | 同面积 | Lines |
| `PlotSeries<T>` | 只有标记（散点） | `Marker`（默认圆）、`MarkerSize`、`MarkerFill`、`MarkerStroke` | Points |
| `BubbleSeries<T>` | 标记的大小由第三个读数决定 | `RadiusSelector` / `RadiusPath` | Points |
| `ColumnSeries<T>` | 柱 | `ColumnWidthRatio`、`MaxColumnWidth`、`Fill`、`Stroke` | Columns |
| `BarSeries<T>` | 条（横放的柱） | 同柱 | Bars |
| `CandlestickSeries<T>` | 蜡烛：实体 + 上下影线 | `Open`/`High`/`Low`/`Close` 选择器、`RiseFill`/`FallFill` 一族 | Candles |
| `HighLowCloseSeries<T>` | 高低收刻线 | 同蜡烛 | Candles |
| `RangeSeries<T>` | 区间（两条读数之间的带） | `LowSelector`、`HighSelector`、`Fill`、`Stroke` | Statistics |
| `ErrorSeries<T>` | 误差条：标记 + 棘线 + 两个帽 | `ErrorSelector` | Statistics |
| `BoxSeries<T>` | 盒须（五数概括） | `MinimumSelector`、`LowerQuartileSelector`、`MedianSelector`、`UpperQuartileSelector`、`MaximumSelector` | Statistics |
| `PieSeries<T>` | 饼：一个读数一块 | `ValueSelector`、`NameSelector`、`InnerRadius`、`StartAngle`、`ExplodeRadius`、`Label*` | Pie |

`PlotSeries<T>` 是 `LineSeries<T>`、`BubbleSeries<T>` 的共同基类（Flex 里这几种各自带一份标记代码，
这里只写了一份）；`BarSeries<T>` 继承 `ColumnSeries<T>`（条形就是横放的柱，和 Flex 同构）；
`CandlestickSeries<T>` 与 `HighLowCloseSeries<T>` 继承 `PriceBarSeries<T>`；
区间、误差、盒须继承 `StatisticalSeries<T>`。

## 线、面积与标记

```csharp
var line = new LineSeries<Reading>
{
    DisplayName = "Depth",
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,

    Stroke = Brushes.SteelBlue,   // 不给就用调色板给的那支
    StrokeThickness = 1.5,
    Curve = CurveMode.Smooth,     // 默认 Linear：两点之间没测过，就直着连
    Marker = Markers.Diamond,     // 折线默认没有标记
    MarkerSize = 4,
};
```

- `CurveMode.Linear` 是默认，也是"两点之间没有被测过"的诚实画法；`Smooth` 是过每一点、且**只落在相邻两点
  之间**的圆滑曲线 —— 不会造出数据里没有的峰。
- 五种标记随库发布：`Markers.Circle`（默认）、`Square`、`Diamond`、`Cross`、`Triangle`；
  `Marker = null` 就是没有标记（折线的出厂值）。形状是一个接口（`IMarkerRenderer`），自己写一个见
  [扩展](extension.md)。
- `AreaSeries<T>` 是折线再加一块填充到底边的区域。**缺值处区域断开**，不会就近取一个有效值把洞补上
  （`Flex 对照`：Flex 的 `findClosestValidValue` 在这里被明确拒绝 —— 那是在数据里编一个不存在的数）。

## 柱、条、并排与堆叠

```csharp
var sales = new CartesianChart { SeriesLayout = SeriesLayout.Cluster };
sales.Series.Add(new ColumnSeries<Sale> { XSelector = s => s.Month, YSelector = s => s.ThisYear, DisplayName = "This year" });
sales.Series.Add(new ColumnSeries<Sale> { XSelector = s => s.Month, YSelector = s => s.LastYear, DisplayName = "Last year" });
```

带宽（一个槽位有多宽）由图表算，不由你算：柱系列在 `DescribeData` 里为每个记录声明"这个槽位要半格余量"，
于是一组柱的第一个和最后一个不会被切成一半。三个旋钮：

| 旋钮 | 默认 | 作用 |
|---|---|---|
| `SeriesLayout` | `Overlay` | `Cluster` 并排 · `Overlay` 各自占满整个槽位 · `Stacked` 累加 |
| `ColumnWidthRatio` | `0.65` | 柱占槽位的比例（Flex 的 `columnWidthRatio`） |
| `MaxColumnWidth` | 无 | 柱最宽多少像素；拖宽窗口时柱不会变成一块砖 |

`Overlay` 是默认，也是 Flex 的默认 —— 所以"柱状图上叠一条线"天生成立：线不占槽位，柱站满整个槽位。
**只有带宽系列参与布局**（柱、条、蜡烛、高低收、区间、误差、盒须），折线不占位。

堆叠（`SeriesLayout.Stacked`）按声明顺序折叠：正数往上堆、负数往下堆，轴的值域长到最大的那一摞。
堆叠里**缺值仍然不是零**：它不画高度，但数据提示分得清"0"和"没有数据"。

`Flex 对照`：Flex 造"悬空柱"（floating column）靠每个数据点上的 `minField`。这里没有逐点的底座属性；
要一个非零起点，就用堆叠（`IStackable`，见[扩展](extension.md)），或者把这一段画成 `RangeSeries`。

## 金融：蜡烛与高低收

```csharp
var candles = new CandlestickSeries<Bar>
{
    XSelector = bar => bar.Session,
    CloseSelector = bar => bar.Close,     // 不给就是记录自己的值
    OpenSelector = bar => bar.Open,
    HighSelector = bar => bar.High,
    LowSelector = bar => bar.Low,
};
```

涨（收 ≥ 开）与跌（收 < 开）两种颜色来自主题的两个令牌（`FinancialUp` / `FinancialDown`），也可以用
`RiseFill`/`RiseStroke`/`FallFill`/`FallStroke` 单独指定。它们来自令牌而不是调色板的槽位，因为
"这根涨还是跌"是读数自己的事，和这个系列在几个系列里排第几无关。

`HighLowCloseSeries<T>` 画的是同一条信息不带实体的样子：一条从最低到最高的线，加收盘与开盘两个刻度。

## 统计：区间、误差条、盒须

三种系列都在问同一个问题："这一组读数的离散程度是多少"。

- **`RangeSeries<T>`** 画两条读数之间的带（`LowSelector`/`HighSelector`），适合置信区间、上下限。
- **`ErrorSeries<T>`** 在读数上画一条棘线和两个帽，误差值用**数据自己的单位**给（`ErrorSelector`）。
- **`BoxSeries<T>`** 用五数概括画盒须图：最小值、下四分位、中位数、上四分位、最大值。中位数就是记录自己的
  值，其余四个不给就取中位数（于是它退化成一个标记）。

## 饼与玫瑰

```csharp
var pie = new PieChart { Height = 260 };
pie.Series.Add(new PieSeries<Share>
{
    ValueSelector = share => share.Amount,
    NameSelector = share => share.Name,
    InnerRadius = 0.55,                    // 圆环
    StartAngle = 90,                       // 第一块从十二点开始
    ExplodeSelector = share => share.IsKey ? 0.08 : 0,
    LabelSelector = share => share.Name,
    LabelPosition = PieLabelPosition.Outside,
});
```

| 属性 | 含义 |
|---|---|
| `ValueSelector` | 这个读数占整体的多少（只有画得出楔形的读数参与总和） |
| `NameSelector` | 数据提示里它叫什么；不给就按位置叫 |
| `InnerRadius` / `OuterRadius` | 内、外半径，都是圆半径的**比例**；`0.55` 就是圆环 |
| `StartAngle` / `Clockwise` | 第一块从哪儿开始（度，0 是三点钟，角度逆时针增长）；方向默认逆时针，和 Flex 的饼一样 |
| `ExplodeRadius` | 整圈往外推多少（圆半径的比例）；`ExplodeSelector` 是**逐块**再推一点，两者相加 |
| `ReserveExplodeRadius` | 推出去的那块要不要让整圆先缩小，好让它仍在图内（默认否） |
| `RadiusByValue` | 楔形的长度（而不是宽度）表示读数：这就是玫瑰图 |
| `LabelSelector` / `LabelPosition` | 楔形上写什么字（`Inside` 写在块里、`Outside` 写在旁边带引线）；默认不写 |
| `LabelImageSelector` / `LabelImageSize` | 标签旁的小图片（产品 logo、浏览器图标之类）；它是个 `IImage`，所以矢量图要宿主先栅格化 |

两件容易误读的事，都写在 `RadiusByValue` 的注释里：玫瑰图的半径是**线性**比例，所以一块的面积并不等于读数
（南丁格尔原图用的是半径的平方根）；另外缺值的记录不画楔形、也不占圆的份额，剩下的块自己分。

一个饼的颜色来自调色板里**逐记录**取槽（第一个记录拿这个系列本来会拿的那一槽，往后依次循环），所以饼旁边
放一条线时它们不会撞色。一张 `PieChart` 放好几个饼就是嵌套的环：图表把圆分成几圈交给系列
（`SeriesBase.TakeBand`），宿主不必自己算 `InnerRadius`。

## 组合图与第二根纵轴

同一个绘图区上放几样东西，就是往 `chart.Series` 里多 `Add` 几次；剩下的交给图表：

```csharp
var chart = new CartesianChart { SeriesLayout = SeriesLayout.Stacked };

chart.Series.Add(new ColumnSeries<Reading> { /* 每小时上网 */ });
chart.Series.Add(new LineSeries<Reading>
{
    /* 成功率，单位是 %，量纲和柱子完全不同 */
    VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100, Title = "%" },
});
```

系列自带一根轴（`HorizontalAxis`/`VerticalAxis`，配 `SetAxis`/`AxisFor`），于是它被**那根轴**量、被那根轴
标注：这就是第二把标尺。三条规则：

- 图表自己那根轴画在近边（下/左），系列带来的第一根额外轴画在**远边**（上/右）—— 读者就是在那里找它，
  两套标签也不会打架。
- 额外轴的**网格关掉**：同一块绘图区上再来一套网格线，说不出第一套没说过的话。
- **每个维度只画一根额外轴**。第三把尺不会凭空得到一个位置：用它的系列照旧被它摆放，只是没有自己的标签轴。

`Flex 对照`：Flex 的第二条纵轴要往 `LineChart` 这种容器类的 `verticalAxisRenderers` 里塞渲染器。这里只需
要"这个系列用这根轴"，渲染是由图表按同一套规则接上去的。

`Series.Visible = false` 把系列整个撤出这张图：不画、不进轴的值域、不参与堆叠和带宽、自己带来的轴也收走、
命中测试与数据提示都找不到它。图例点一下做的就是这件事（见[图例、元素与导出](formatting.md)）。

## 一个轴实例服务好几张图

轴**只持有设置，从不持有结果**（`Resolve` 是纯函数），所以一根 `DateTimeAxis` 实例可以同时给几张图用：

```csharp
var across = new DateTimeAxis { Title = "Session" };
price.HorizontalAxis = across;
volume.HorizontalAxis = across;
```

于是两张图看到的是同一段时刻，拖着滚轮缩放其中一张，另一张跟着动 —— 轴持有设置，两张图各自按它算自己的
绘图区。

**绘图区还要自己对齐一次。** 每张图给纵轴留的地方是**它自己标签**的宽度（价格 `110` 和成交量 `2,000`
不一样宽），于是同一个时刻落在两边的不同列上，刻度也就错开了。解法是两行算术：读出每张图实际让出了多少，
把最大的那个作为下限交给两边。

```csharp
// 每张图让出的宽度 = 它自己边界到绘图区左边的距离
var widest = Math.Max(price.Bounds.X, volume.Bounds.X);
price.VerticalRenderer.MinimumGutter = Math.Max(0, widest - price.Bounds.X);
volume.VerticalRenderer.MinimumGutter = Math.Max(0, widest - volume.Bounds.X);
```

`AxisRenderer.MinimumGutter`（默认 0）就是"这根轴至少要留多少房间，不管标签需要多少"。它不自动做，因为
一张图并不知道自己是几张里的一张；画廊的 Panes 页是完整的例子（一个 `Panel` 读两边的 gutter、设好、再布局
一次，外加一个把两种状态摆出来的旋钮）。

---

[English version](../en/using/series.md)
