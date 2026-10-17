# 扩展

扩展不是一棵继承树，而是几个各管一件事的接口。先决定你要改哪一层，再写最少的那点代码：

| 想改什么 | 改哪里 |
|---|---|
| 标记长什么样（另一种点） | `IMarkerRenderer`：一个方法，往批量里加一个形状 |
| 线之间怎么画（另一种连线） | 继承 `PlotSeries<T>` / `LineSeries<T>`，覆盖 `BuildInk` |
| 一种新的读数规则与画法 | 继承 `CartesianSeries<T>`，写几个钩子 |
| 一种新的刻度（梯级、标签） | 继承 `AxisBase`，写两个映射方法，按需再写一个梯级方法 |
| 另一套坐标（极坐标之类） | 继承 `DataTransform`，把它交给 `chart.Transform` |
| 不是系列的整块东西（阈值、注记、承载控件） | 继承 `ChartElement`，放进 `chart.Elements` |
| 一整层的绘制 | 继承 `ChartLayer`，用 `AddLayer` 挂上去 |

## 自定义系列：四个钩子

`SeriesBase` 上的四个钩子是契约，**顺序是**：`UpdateData`（读源）→ `DescribeData`（说轴要覆盖什么）→
`BuildGeometry`（把值变成墨，每次变更建一次，不是每帧）→ `Draw`（把墨画上去）。顺序被测试钉着：不按顺序来
的系列，会看到一根按它已经没有的数据量出来的轴。

实际上你很少从 `SeriesBase` 开始。派生 `CartesianSeries<T>` 就已经拿到了：`ItemsSource` 与选择器、`Filter`、
增量更新、`Push`、`MaxReadings`、采样、按轴的映射、数据提示那行字、`Items` 视图、按线段的命中测试 ——
`UpdateData` 与 `DescribeData` 的默认实现也在里面。**通常只剩下一件事**：把点画成你的样子。

一个完整的例子 —— 茎叶图：从轴自己的零点画一条竖线到每个读数，再点一个圆点。

```csharp
public sealed class StemSeries<T> : CartesianSeries<T>
{
    private Geometry? _stems;

    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var zero = ZeroOn(ChartDimension.Y);     // 轴给的零点；对数轴没有零点，就是 NaN
        var batch = new GeometryBatch();

        foreach (var (_, x, y, point) in PlacedRecords(context))   // 缺值的记录不会出现在这里
        {
            if (!double.IsNaN(zero) && context.TryUnitPoint(x, zero, out var foot))
            {
                batch.AddPolyline([foot, point]);                  // 两个点就是一条线段
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

几条规矩：

- `SeriesRenderContext` 给你的是"绘图区在哪、怎么把值变成位置、怎么把线对齐到像素"：`PlotBounds`、
  `Transform`、`RenderScaling`、`TryPoint(x, y, out point)`、`TryUnitPoint(x, y, out point)`、`Snap` /
  `SnapStroke`、`Bands`。系列从不自己去问图表的尺寸 —— 所以同一份系列代码在控件里、在无头测试里都成立。
- **几何缓存在字段里**：建一次、画很多次，只有映射变了才重建。这是"平移一帧不重建几何"的前提，本库自己
  那几条系列也是这么写的。
- 想要自己的命中形状就覆盖 `HitTestItem`（默认按线段距离算），想要自己的记录视图就覆盖 `Items`。
- 颜料从哪来：`PaletteStroke` / `PaletteFill` 是图表按声明顺序发给它的；**自己带了颜色就覆盖
  `HasOwnColours`**，这样它不占调色板的槽。
- 要它参与堆叠，就实现 `IStackable`；要它站进带宽（柱、条那一类），就实现 `IColumn`。

## 自定义标记

```csharp
public sealed class StarMarker : IMarkerRenderer
{
    public void AddTo(GeometryBatch batch, Point centre, double radius) =>
        batch.AddPolyline(StarPoints(centre, radius), isClosed: true);
}

line.Marker = new StarMarker();
```

接口只有一个方法，交给你的是一个 `GeometryBatch`：`AddPolyline`、`AddCurve`、`AddRectangle`、`AddCircle`、
`AddRing`、`AddDiamond`、`AddCross`、`AddTriangle`、`AddWedge`，最后 `Build()` 一次把整批变成一份几何。
整批一次画完，比一个点建一份 `Geometry` 便宜得多。

`Flex 对照`：Flex 里"给 ChartItem 换一种皮肤"是写一个图形或程序化的 itemRenderer 挂在系列上。这里拆成
两件事 —— 换标记形状实现上面这个接口就够了（不需要新的系列类型），换整条线的画法才写系列子类。

## 自定义轴

一根轴最少要回答两个问题：值怎么变成单位空间的数（`TryToUnit`），以及反过来（`FromUnit`）。梯级与标签都
有默认，需要时再覆盖：

```csharp
/// 以 2 的幂为刻度的轴：单位空间是 log2(值)，刻度落在 1、2、4、8…
public sealed class BinaryAxis : AxisBase
{
    public override ChartValueKind ValueKind => ChartValueKind.Number;

    public override bool TryToUnit(ChartValue value, out double unit)
    {
        unit = 0;

        // 0 与负值在这根轴上没有位置：返回 false 就是"这个维度没有值"，
        // 而不是把它们挪到某个看起来可以的地方（对数轴也是这么办的）。
        return value.TryGetNumber(out var number) && number > 0
            && double.IsFinite(unit = Math.Log2(number));
    }

    public override ChartValue FromUnit(double unit) => Math.Pow(2, unit);

    /// 一格就是整数个八度（二进制下的"数量级"）
    protected override double NiceInterval(double target) => Math.Max(1, Math.Round(target));

    /// 标签写值本身，而不是它在这个空间里的指数
    public override string FormatValue(double unit) =>
        Math.Pow(2, unit).ToString("G4", CultureInfo.InvariantCulture);
}
```

覆盖点各有分工：

| 覆盖什么 | 什么时候 |
|---|---|
| `TryToUnit` / `FromUnit` | 总是（这是"它是什么轴"） |
| `NiceInterval` | 刻度等距，只是要落在另一组可读的数上（默认是 1 / 2 / 2.5 / 5 × 10ⁿ） |
| `ResolveStep` | 刻度**不等距**（时刻轴按月走就是它），返回自己的 `AxisStep`；`EvenStep` 是最常见的那个 |
| `FormatValue(unit)` / `FormatValue(unit, step)` | 标签要按自己的规矩写；看步长的那一档是给"一格一个月就写年月"这种用的 |

轴**只持有设置，从不持有结果**（`Resolve` 是纯函数），所以自定义轴也可以是共享实例，同时给几张图量。

## 图表元素

不是系列的整块东西 —— 阈值线、参考区、事件标记、一张承载按钮的注记：

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

四件事：

- 元素画在**系列之下**（它在 `AnnotationLayer` 里），所以注记不会盖住读数；`AxisSection` 那一种反过来写在
  轴标签之上。
- 默认**不为元素撑开轴的值域**（阈值线通常就该在量程之外）；要它算进去就覆盖 `DescribeData`。
- 位置在 `Draw` 里现算，或者缓存在字段里、在 `MappingChanged()` 里重算 —— 那是"映射动了"的通知。
- 元素变了调一次 `Invalidate()`，这一层就重画。

**承载一件真控件**：`Visual` 交出一个 `Layoutable`，`VisualAnchor` 说它的**中心**站在哪个像素上。放在提示
旁边的按钮、一张小卡片就是这么来的 —— 能按、能拿焦点、能穿宿主自己的主题，这是画出来的形状做不到的。
代价是视觉树里的一个真控件，所以是"几件"，不是每个读数一件。画廊 Annotations 页有一个完整的例子。

## 另一套坐标：变换

`DataTransform` 负责"值 ↔ 像素"这件事。库里另一套实现是 `PolarTransform`：角度用圈数（从三点钟起、
逆时针增长），半径用圆半径的比例，圆心是绘图区中心，半径取放得下的最大圆。饼图的楔形就是它摆出来的，
而图层、命中测试、数据提示都还在问同样的问题 —— 这就是一个坐标系能这么换的原因。

自己写一个：覆盖 `TryValueToPoint`、`TryUnitToPoint`、`TryPointToValue`（第三个是命中测试与提示要的），
需要的话还有 `UpdateRanges` / `GetDimensionLength`，然后：

```csharp
chart.Transform = new MyTransform();   // 图层、轴、系列、元素都会搬过去
```

这是高级用法：库里其它一切都假设"横着是一个值、竖着是一个值"。

## 一整个图层

要有自己的一层画面（自己的一套家具、自己的一段背景），继承 `ChartLayer` 覆盖 `DrawLayer`，用
`AddLayer(layer)` 挂上去（它是 `protected`，所以从 `ChartBase` 的子类里调）。图层自己带 `PlotBounds` 与
`RenderScaling`（`Configure` 给的），失效粒度就是它 —— 一层重画，不是整张图重画。

---

[English version](../en/using/extension.md)
