# AvCharts 使用指南

这份指南讲**怎么用**：把一份读数变成一张图，再把这张图调成你要的样子。它不解释每个成员做什么 ——
那是 [API 参考](../reference/index.md) 的事 —— 它解释的是"要画出这样一张图，该写哪几行、为什么是这样写"。

覆盖面参照 Apache Flex 的《Using Flex》图表那一章（图表简介 / 图表类型 / 显示数据与标签 / 格式化 /
事件与效果），按 AvCharts 的实际形状重写。凡是两边不一样的地方都写明了差异，用 `Flex 对照` 开头。

## 三十秒上手

两个包。`AvCharts.Controls` 是控件那一半，它依赖 `AvCharts`（模型那一半：轴、变换、系列、绘制内核）。

```pwsh
dotnet add package AvCharts.Controls
```

页面的资源里加一行，这一页上所有图表都装扮好：

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

然后画一条线。数据是**你自己的对象**，用选择器告诉图表读哪两个字段：

```csharp
var chart = new CartesianChart
{
    Height = 240,
    HorizontalAxis = new DateTimeAxis(),
    VerticalAxis = new LinearAxis(),
};

chart.Series.Add(new LineSeries<Reading>
{
    DisplayName = "Depth",
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
});
```

同一张图在 XAML 里是这样：

```xml
<avc:CartesianChart xmlns:avc="https://github.com/slobber/av-charts" Height="240">
    <avc:CartesianChart.HorizontalAxis>
        <avc:DateTimeAxis />
    </avc:CartesianChart.HorizontalAxis>
    <avc:CartesianChart.VerticalAxis>
        <avc:LinearAxis />
    </avc:CartesianChart.VerticalAxis>
    <avc:LineSeries DisplayName="Depth" ItemsSource="{Binding Readings}" XPath="When" YPath="Depth" />
</avc:CartesianChart>
```

系列是图表的**内容**（`CartesianChart.Series` 是 `Content` 属性），所以直接写在图表下面，不用包一层属性
元素。

## 要先知道的四件事

**给图表高度。** 图表的高度由容器给。放在 `StackPanel`、`ScrollViewer` 这类不约束高度的容器里，它会被量成
无限高，于是溢出格子、和邻居叠在一起。放在 `Grid` 的按比例行、`DockPanel` 的填充区，或者直接写
`Height="240"`。

**接上主题。** 图表画的每一种颜色都是资源字典里的一个令牌，没接主题就没有颜色；而且 Avalonia 里**没有可绘制
内容的控件在命中测试中是透明的** —— 图表要有一个 `Background`（哪怕是 `Transparent`）才收得到指针事件，
主题给的 `SurfaceBackground` 正好补上这一点。不接主题的图表：没颜色，而且点它、滚它都没有反应。

**系列是模型，不是控件。** 一个系列不持有一个 `Visual`；持有它的是绘制它的那个图层。这是"一万个点不等于
一万个控件"的原因，也是为什么数据点没有可订阅的事件 —— 每项事件（`ItemClick`、`ItemPointerEntered` 等）
是图表按命中测试的结果自己合成的。

**一切都是"单位空间"。** 每个轴把自己那一个维度的数据值变成一串单调的数（线性轴：值本身；时刻轴：毫秒；
对数轴：十进制指数；分类轴：槽位号；时长轴：毫秒），叠加、采样、命中测试、选择全部只在这个空间里算。
这是"换一根轴，整套东西照样成立"的原因，也是选择器写错时症状往往不明显的原因 —— 值照样被画出来，只是
画在别处。

## 一次绘制都发生了什么

理解这五步，大部分"为什么它不刷新"的问题自己就有答案了。

```
数据源 ──①UpdateData──▶ 单位空间 double[] ──②DescribeData──▶ 各轴的值域
                                    │
                                    └──③BuildGeometry──▶ 几何（像素，每次变更重建一次）
                                                              │
                        布局：轴量出 gutter，算出绘图区 ──④Draw──▶ 图层把它画到屏幕上
                                                              │
                        ⑤命中测试／提示／十字线／选择 ◀────────┘
```

四条规则跟着来：

- **数据变了才重读**（`InvalidateData` 是手动告诉它数据变了）；**几何只在映射变了才重建**；**改颜色只重画**
  （`Series` 自己的注释里写明了这条区别：改变墨色不需要重算形状）。
- **值域是轴算的，不是你算的**。轴默认 `AxisScaleMode.Fit`，按 `DescribeData` 声明的范围自动铺满；给它
  `Minimum`/`Maximum` 才是你说了算。
- **采样发生在建几何之前，只影响画出来的东西**，不改你手上的数据，也不改轴的值域。
- **一个图层一个失效粒度**：悬停一个点重录一个图层，不是整张图。这是"一百万个读数仍然能交互"的基础。

## 本指南的十页，以及它们对应 Flex 的哪一节

| 这一页 | 讲什么 | Flex《Using Flex》里的对应节 |
|---|---|---|
| [第一个图表](first-chart.md) | 装包、装扮、放上页面、XAML 与代码两条路、导出 | 图表简介 · 在 ActionScript 中创建图表 |
| [系列与图表类型](series.md) | 十几种系列、组合图、双 Y 轴、带宽与堆叠 | 图表类型 · 使用多个数据系列 · 使用多个轴 |
| [数据](data.md) | 数据源、选择器、空值、增量更新、后台推送、采样 | 定义图表数据 · 图表数据的结构 · 运行时更改数据 |
| [轴、刻度与视窗](axes.md) | 五种轴、Fit 与 Fixed、刻度与标签、缩放平移 | 使用轴 · 轴标签 · 设置范围 · 定位轴 |
| [主题与调色板](theming.md) | 令牌、Light/Dark、自定义 Palette、系列主题色 | 样式 · 刻度与网格线 · 填充与描边 |
| [图例、元素与导出](formatting.md) | 图例、标记、图上的元素、导出、留白 | 格式化图表 · 图例 · 导出 |
| [交互](interaction.md) | 命中测试、数据提示、十字线、选择、事件、滚动 | 事件与效果 · 使用 HitData · 选择图表项 |
| [扩展](extension.md) | 自定义系列、自定义轴、标记、图表元素、变换 | 自定义呈现器 · 创建自定义 AxisRenderer |
| [性能与大批量读数](performance.md) | 数字、每帧的成本、内存、该调哪个旋钮 | ——（Flex 没有对应内容） |
| [常见问题](troubleshooting.md) | 症状 → 原因 → 做法 | —— |

## 状态

v1 的功能已经完成并发布（`AvCharts` / `AvCharts.Controls` 0.2.5 在 NuGet.org），v1.1 的待办在
[设计简报](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md) 的 §14。这份指南描述的是
**已经能用的**东西，还没做的（动画骨架、分档 zoom）在相关章节里点明。

想先看效果：画廊（`dotnet run --project src/AvCharts.Demo.Desktop`，或浏览器里的
[在线版](https://nandi.wang/av-charts/gallery/)）里每一页都能在这一章里找到对应的一节，页面上的例子可以
直接抄 —— 每个例子都是"结果 / 代码 / axaml"三个页签，axaml 那份就是跑起来的那份。

---

[English version](../en/using/index.md)
