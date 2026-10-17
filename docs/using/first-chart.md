# 第一个图表

这一页把"从零到一张图"走完：装包、装扮、画出来、放到页面上、导出成图片。每一节都说清了它为什么是这个
样子，因为这几步里踩过的坑（高度、主题、选择器）恰好也是后面每一页都会回来看的。

## 1. 装包

```pwsh
dotnet add package AvCharts.Controls
```

`AvCharts.Controls` 依赖 `AvCharts`，两个都会被装上。分界是有意义的：

- **`AvCharts`** 是模型：轴、变换、系列、绘制内核、几何批量。它只用到 Avalonia 的**基础那一半**
  （`Avalonia.Base`），不认识 `Avalonia.Controls`，所以可以在一台没有 UI 的机器上算、在测试里跑、
  在别的宿主里复用。
- **`AvCharts.Controls`** 是控件那一半：`ChartBase : Control`、`CartesianChart`、`PieChart`、数据提示、
  图例、滚动条、概览、控制主题与颜色令牌。

## 2. 装扮

图表画的每一种颜色都来自一个**令牌**（token），令牌在一个资源字典里，带浅色和深色两套。接上它有两种写法。

XAML 里一行，这一页上所有图表都装扮好：

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

代码里构建资源的时候用 `ChartTheme.Create()`：

```csharp
Application.Current!.Resources.MergedDictionaries.Add(ChartTheme.Create());
```

如果宿主有很多窗口、不想每个都写一遍，还有一条**全局默认**的路子 —— 在 `Main` 或 `App` 里调一次，
之后建出来的每一张图都从它开始：

```csharp
ChartDefaults.Configure(defaults =>
{
    defaults.Theme = ChartTheme.Create();
    defaults.Typeface = new Typeface("Inter");
});
```

两件事值得知道：

- `Typeface` 是给**轴的标签**用的，因为标签由渲染器画、上面没有可以继承字体的控件。画中文标签的图表要自己
  给一个带汉字的字体（画廊的浏览器版为此内嵌了一个经过裁剪的 Noto Sans SC，见 `DemoFonts`）。
- 这是**启动时**的调用。已经在屏幕上的图表保持它被装扮时的样子，直到它自己的资源变了 —— 和别的主题一个
  规则。

## 3. 画出来

代码里：

```csharp
using AvCharts.Axes;      // 轴
using AvCharts.Controls;  // 图表控件
using AvCharts.Series;    // 系列

var chart = new CartesianChart
{
    Height = 240,
    HorizontalAxis = new DateTimeAxis { Title = "Time" },
    VerticalAxis = new LinearAxis { Title = "Depth, m" },
};

chart.Series.Add(new LineSeries<Reading>
{
    DisplayName = "Depth",
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
});
```

`CartesianChart` 出厂时两根轴都是 `LinearAxis`，所以只写 `HorizontalAxis`/`VerticalAxis` 里你想改的那一根
也行。

同一个图在 XAML 里：

```xml
<avc:CartesianChart xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:avc="https://github.com/slobber/av-charts"
                    Height="240">
    <avc:CartesianChart.HorizontalAxis>
        <avc:DateTimeAxis Title="Time" />
    </avc:CartesianChart.HorizontalAxis>
    <avc:CartesianChart.VerticalAxis>
        <avc:LinearAxis Title="Depth, m" />
    </avc:CartesianChart.VerticalAxis>
    <avc:LineSeries DisplayName="Depth"
                    ItemsSource="{Binding Readings}"
                    XPath="When"
                    YPath="Depth" />
</avc:CartesianChart>
```

关于 XAML 的几件事：

- **前缀**：命名空间是 `https://github.com/slobber/av-charts`，建议前缀 `avc`（库自己声明的也是它）。
- **系列是内容属性**，直接写在图表下。
- **十四个系列都有非泛型的版本**（`LineSeries`、`ColumnSeries`、`PieSeries`…），专为标记文件准备：泛型参数
  在 XAML 里写不出来，非泛型版本就是 `XxxSeries<object>`，数据用**路径**读而不是用 lambda 读。
- **路径选择器**：`XPath`/`YPath` 之外，各系列还有自己那几个 —— `RadiusPath`（气泡）、
  `OpenPath`/`HighPath`/`LowPath`/`ClosePath`（蜡烛与高低收）、`LowPath`/`HighPath`（区间）、
  `ErrorPath`（误差）、`MinimumPath`/`LowerQuartilePath`/`MedianPath`/`UpperQuartilePath`/`MaximumPath`
  （盒须）、`ValuePath`/`NamePath`/`LabelPath`/`ExplodePath`（饼）。路径走属性、字段、字典条目，
  看见 `List<T>` 会按索引取，最后一步的值按 `ChartValue` 的规则理解（数值、时刻、字符串）。
- **同一个图的两半**：图形与代码两条路完全等价，画廊里每一个例子都是这么写的 —— 一对同名文件，`.axaml`
  是给你抄的那份，`.cs` 是同一张图用代码搭出来；测试逐像素比对两张图，所以它们不会各自漂移。

## 4. 放到页面上

三条布局上的规矩：

**给高度。** 放在 `StackPanel`、不约束高度的 `ScrollViewer` 里，图表会被量成无限高，于是压住邻居。用
`Grid` 的 `*` 行、`DockPanel` 的填充区，或者一个明确的 `Height`。

**让轴自己留地方。** 绘图区 = 控件边界减去两边轴要的 gutter（刻度、标签、标题）。标签越宽、标题越长、
字号越大，绘图区越小，这些都不用你算；收缩绘图区也不会把标签切掉。刻度标签在最外端会向外超出半个标签，
这半个标签也算进 gutter（`AxisRenderer.MeasureOverhang`）。

**尺寸小就少放东西。** 200×120 的图放两根带标签的轴只剩一点点地方；这种尺寸要么去掉标签
（`ShowLabels = false`），要么把标签转过来（`LabelRotation`），要么把间距调稀（`MajorGridPitch`）。

## 5. 图表和系列：一张图可以有几样东西

任何笛卡尔系列都能放在**任何** `CartesianChart` 上，所以"柱状图"不是一种图表类，而是"一张
`CartesianChart` 上放了一组 `ColumnSeries`"。这也是组合图天然成立的原因：

```csharp
chart.Series.Add(new ColumnSeries<Sale> { /* … */ });
chart.Series.Add(new LineSeries<Sale> { /* … */ });   // 同一条横轴上叠一条线
```

`Flex 对照`：Flex 有 `LineChart`、`ColumnChart`、`AreaChart` 这些类型类。这里没有移植它们，因为
`LineChart.as` 主要就是给系列套样式，而 `ColumnChart` 真正的本事（并排、堆叠）在本库里是**图表的布局**
（`CartesianChart.SeriesLayout`），于是它作用于每一张图，而不是某一个以图型命名的类。

饼图是另一个类别：它用 `PieChart` + `PieSeries<T>`；一张图放好几个饼，画出来就是嵌套的环。

## 6. 拿出去

同一张图可以按你指定的尺寸**重新布局**出一张位图：

```csharp
RenderTargetBitmap image = chart.RenderImage(new PixelSize(1200, 600));
image.Save("depth.png");

// 打印要的是更多像素和更多细节，不是放大后的模糊：
using var stream = File.Create("depth-print.png");
chart.SaveImage(stream, new PixelSize(800, 400), scaling: 2);

// 浏览器里的文件句柄只认异步写：
await chart.SaveImageAsync(stream, new PixelSize(800, 400), scaling: 2);
```

一个坑写在明处：`scaling` 大于 1 时位图的 dots-per-inch 不再是 96，而 Avalonia 的 `Image` 会把这种位图按
**像素尺寸**画而不是按你给它的尺寸画（`Stretch` 的三种取值都一样），于是预览里只看见图片的一个角。
预览要自己缩放着画 —— 画廊的 Export 页就是这么做的。

## 接下来

- 换一种图看[系列与图表类型](series.md)；
- 数据从哪来、缺值怎么画、十万个点怎么办，见[数据](data.md)；
- 横轴为什么没有按你想的排，见[轴、刻度与视窗](axes.md)。

---

[English version](../en/using/first-chart.md)
