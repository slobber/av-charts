# 外观与主题

图表画的每一种颜色都是一个**令牌**，令牌在一个资源字典里；每个控件的模板都在一个 `ControlTheme` 里。
这一页讲怎么接上它们、怎么改一个、怎么整套换掉，以及颜色之外的几件事：调色板、图例、图上的元素、导出。

## 主题在哪、怎么接

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

```csharp
// 或者，在代码里构建资源的时候：
Application.Current!.Resources.MergedDictionaries.Add(ChartTheme.Create());

// 或者，一次配置、全局生效：
ChartDefaults.Configure(defaults => defaults.Theme = ChartTheme.Create());
```

三者的关系：`ChartDefaults.Current.Theme` 是**最后**被查的地方（图表自己的资源范围先查），所以给某一个窗口
单独放一份主题，那个窗口就用那一份。`ChartDefaults` 是启动时的调用，屏幕上的图表保持它被装扮时的样子。

## 令牌

令牌的键是 `AvCharts.Themes.ChartTokens` 上的公共常量，按用途分组：

| 组 | 令牌 |
|---|---|
| 表面 | `SurfaceBackground`、`SurfaceBorder`、`SurfaceFocusRing` |
| 轴 | `AxisLine`、`AxisTick`、`AxisTickMinor`、`AxisGridLineMajor`、`AxisGridLineMinor`、`AxisGridLineOrigin`、`AxisLabelForeground`、`AxisTitleForeground`、`AxisBandFill`、`AxisBandFillAlt` |
| 标记 | `MarkerFill`、`MarkerStroke`、`MarkerHoverFill`、`MarkerHoverStroke`、`MarkerSelectedFill`、`MarkerSelectedStroke` |
| 十字线 | `CrosshairLine`、`CrosshairLabelBackground`、`CrosshairLabelForeground` |
| 数据提示 | `DataTipBackground`、`DataTipBorder`、`DataTipForeground`、`DataTipShadow` |
| 图例 | `LegendBackground`、`LegendBorder`、`LegendForeground`、`LegendItemDisabled` |
| 滚动条与概览 | `ScrollbarTrack`、`ScrollbarThumb`、`ScrollbarThumbHover`、`OverviewBackground`、`OverviewWindowFill`、`OverviewWindowStroke` |
| 金融 | `FinancialUp`、`FinancialDown` |
| 框选 | `SelectionRubberBandFill`、`SelectionRubberBandStroke` |

字典带**浅色和深色两套**（`ThemeDictionaries`），图表跟着它所在范围的变体走。一张深色图放在浅色页面上
不是重画一遍，而是一个 `ThemeVariantScope` 的事。

**改一个令牌**就是"同一个键，放你自己的值"：

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
    <SolidColorBrush x:Key="AxisGridLineMajor" Color="#1A000000" Opacity="0.35" />
</Application.Resources>
```

**整套换掉**就是写自己的 `ControlTheme` 挂在同一个类型键下（`ChartBase`、`DataTip`、`Legend`、
`LegendItem`）。`ControlTheme` 是"这个控件长什么样、默认属性是什么"的落点：库自己那一份把令牌接到
`ChartBase` 的属性上（背景、边框、焦点环、轴线、刻度、网格、次网格、标签、标题、十字线、悬停与选中的墨色、
框选的墨色）。其中 `Background` 除了好看还有功能：没有可绘制内容的控件在命中测试里是透明的，`Transparent`
也是画刷，这就是"图表收得到指针事件"的前提。

`Flex 对照`：Flex 用 CSS 类选择器和 `AxisRenderer` 上的样式属性来打扮图表。这里没有 CSS —— 令牌 + XAML
样式 + `ControlTheme` 就是同一件事在这边的形状，而且"样式写在谁的资源范围里，就管到谁"这条比 CSS 的
全局作用域好推理。

## 调色板：谁拿哪一支颜色

十支颜色的顺序在主题里。图表按**系列声明的顺序**发槽：

```csharp
chart.Series.Add(line);        // 第 1 槽
chart.Series.Add(area);        // 第 2 槽
chart.Series.Add(column);      // 第 3 槽
```

- **自己带了颜色的系列不占槽**：第三行里如果 `area.Fill` 是显式设的，`column` 拿到的还是第 2 槽。
  （`Flex 对照`：Flex 的规则是给第一个系列设颜色之后，后面每个系列的颜色都跟着挪一格 —— 那是个让人
  改一处、错一处的规则。）
- 系列比槽多时**循环取用**。
- 按点取色有出口：`chart.PaletteStroke(index)` / `chart.PaletteFill(index)`；饼图自己就是按记录索引取色的。
- 系列的 `Color` 属性给它实际在画的那个颜色，所以图例里的小色块和提示旁边的色块永远与线一致。

## 每个系列的墨色

| 系列 | 画什么用什么 |
|---|---|
| `LineSeries<T>` | `Stroke`、`StrokeThickness` |
| `AreaSeries<T>` | `Fill`（区域）、`Stroke`（线） |
| `PlotSeries<T>` | `MarkerFill`、`MarkerStroke`、`Marker`、`MarkerSize` |
| `ColumnSeries<T>` / `BarSeries<T>` | `Fill`、`Stroke` |
| `RangeSeries<T>` | `Fill`、`Stroke` |
| `BoxSeries<T>` | `Fill`、`Stroke`（中位线、须、帽都用它） |
| `ErrorSeries<T>` | `Fill`（标记）、`Stroke`（三根细线只取它的画刷） |
| `CandlestickSeries<T>` / `HighLowCloseSeries<T>` | `RiseFill`/`RiseStroke`、`FallFill`/`FallStroke`，不给就用主题的涨跌两色 |
| `PieSeries<T>` | 逐记录取调色板；`LabelBrush`、`LabelFontSize`、`LabelTypeface` 管标签 |

一个细节值得说清：画细线的系列（误差条、蜡烛的影线）**只取笔的画刷**，不取笔的粗细 —— 一格一格的刻度
是在标一个电平，不是在盖住它。

## 图例

```csharp
var legend = new Legend { Chart = chart, Placement = LegendPlacement.Right };
```

`Legend` 是一个 `ItemsControl`：它把被指向的图表的系列列成行，每行是 `LegendItem`（模板控件，里面有
色块、名字、以及"这一系列正被关掉"的状态）。点一行就把那个系列的 `Visible` 翻过来：

```csharp
chart.Series[0].Visible = false;    // 图例点一下做的就是这件事
```

被关掉的系列**什么都不参与**：不画、不进轴的值域、不参与堆叠与带宽、自己带来的轴也收走、命中测试与提示
也找不到它。它保留的只有"在系列列表里的位置"，所以再打开就是一个属性的事。

四个方位（`Top`/`Bottom`/`Left`/`Right`，默认 `Right`）决定行的方向；图例不进绘图区，放在图表旁边的
`Grid`/`DockPanel` 格里即可。

## 图上的元素

不是系列的东西都住在这里：阈值、参考区、事件标记、宿主自己的东西。

```csharp
chart.Series.Add(readings);

chart.Elements.Add(new Section                 // 绘图区里的一段
{
    Dimension = ChartDimension.Y,
    From = 3,
    To = 6,
    Name = "target",
    Fill = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4), 0.12),
});

chart.HorizontalRenderer.AddSection(new AxisSection   // 轴上的一段
{
    From = 4,
    To = 6,
    Name = "rollout",
});
```

两条规则：

- **元素画在系列之下**（它在 `AnnotationLayer` 里，图层顺序是定的），所以带名字的分段不会盖住读数；
  轴线上的分段反过来写在标签之上。
- `Section` 的两端都可以留空，留空的那端延伸到绘图区的边；`AxisSection` 同理，留空的那端到轴的末端。
  `Name` 会在里面写一行字，被它罩住的刻度标签会让位。

阈值线和事件标记是各自十几行的自定义元素，画廊的 Annotations 页有两个完整的例子（虚线阈值、竖线加一行
小字），写法见[扩展](extension.md)。

**元素还能承载一件真控件**：`ChartElement.Visual` 交出一件 `Layoutable`，`VisualAnchor` 说它站在哪个像素
上，图表就把它摆在那个位置。写在提示旁边的一个按钮、一张小卡片都是这么来的。要点是"几件，不是每个读数
一件"—— 每件都是图表视觉树里的真控件，能拿到键盘。

## 导出

```csharp
chart.SaveImage(File.Create("depth.png"), new PixelSize(1200, 600));
chart.SaveImage(stream, size, scaling: 2);        // 打印：两倍像素、两倍细节
await chart.SaveImageAsync(stream, size);          // 浏览器里的文件句柄只认异步写
```

它是按你给的尺寸**重新布局**一遍再出图，不是把屏幕上的图放大：轴、标签、采样都会按新尺寸重算。
`scaling` 大于 1 时位图的 dots-per-inch 不再是 96，Avalonia 的 `Image` 会按像素尺寸画它 —— 预览要自己
缩放着画（画廊 Export 页有现成的写法）。详见[第一个图表](first-chart.md#6-拿出去)。

## 留白

图表本身不留 padding：绘图区就是控件边界减去轴要的 gutter。要在图表外面留空，给它外面的容器加 margin；
要让轴少占地方，动的是轴的装饰（`LabelMargin`、`TickLength`、`FontSize`、`ShowLabels`、
`LabelRotation`），它们都会触发重新布局。

---

[English version](../en/using/formatting.md)
