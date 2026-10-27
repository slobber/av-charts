# 主题与调色板

AvCharts 的主题分成两层：**资源令牌**决定图表的表面、轴、文字、提示和系列颜色；**`ControlTheme`**
决定控件使用哪些模板和默认属性。日常换肤绝大多数时候只需要改资源令牌，不用改控件，更不用改系列的数据代码。

这一页讲主题怎样接进来、Light/Dark 怎样工作、Palette 怎样分配颜色、怎样定义自己的 Palette，以及某个系列
怎样用自己的颜色覆盖主题。

## 接入默认主题

在 XAML 中把默认资源字典合并到应用、窗口或页面：

```xml
<Application.Resources>
    <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
</Application.Resources>
```

如果宿主在代码里构建资源，可以合并一份新的默认主题：

```csharp
Application.Current!.Resources.MergedDictionaries.Add(ChartTheme.Create());
```

也可以只在图表构造前设置全局默认：

```csharp
ChartDefaults.Configure(defaults => defaults.Theme = ChartTheme.Create());
```

令牌查找顺序是**图表自己的 `Resources` → 逻辑树上的父级资源 → `Application.Resources` →
`ChartDefaults.Current.Theme`**。因此窗口可以覆盖应用级主题，单张图表也可以只改自己的颜色。

主题还承担一个交互前提：Avalonia 中没有可绘制内容的控件不参与命中测试。主题的 `Surface.Background`
给图表一个可见或透明的背景，指针事件才能落到图表上。没有主题时，图表通常既没有预期颜色，也收不到悬停、
点击和滚轮事件。

## 令牌：主题里的公共颜色名

令牌键定义在 `AvCharts.Themes.ChartTokens`。键名是公共 API，可以直接写字符串，也可以在代码里使用常量或
`SeriesStroke` / `SeriesFill` 方法。

| 用途 | 主要令牌 |
|---|---|
| 图表表面 | `SurfaceBackground`、`SurfaceBorder`、`SurfaceFocusRing` |
| 轴与网格 | `AxisLine`、`AxisTick`、`AxisGridLineMajor`、`AxisGridLineMinor`、`AxisLabelForeground`、`AxisTitleForeground` |
| 系列调色板 | `SeriesStroke(1..10)`、`SeriesFill(1..10)` |
| 标记与交互 | `MarkerHoverFill`、`MarkerHoverStroke`、`MarkerSelectedFill`、`MarkerSelectedStroke` |
| 十字线与提示 | `CrosshairLine`、`CrosshairLabelBackground`、`DataTipBackground`、`DataTipBorder`、`DataTipForeground` |
| 图例与概览 | `LegendBackground`、`LegendBorder`、`LegendForeground`、`OverviewWindowFill` |
| 金融与框选 | `FinancialUp`、`FinancialDown`、`SelectionRubberBandFill`、`SelectionRubberBandStroke` |

令牌有两种值类型，不能互换：

- **填充/画刷**是 `IBrush`，例如 `SolidColorBrush`。表面、文字、区域和标记内部使用它。
- **描边**是 `Pen`，它同时带画刷、粗细、虚线等设置。轴线、网格线和调色板中的
  `Series.Stroke1..10` 使用它。

例如只改当前资源范围内的主轴网格：

```xml
<SolidColorBrush x:Key="Axis.GridLine.Major"
                 Color="#1A000000" />
```

## Light、Dark 与 `ThemeVariantScope`

默认主题在 `ThemeDictionaries` 中提供了 `Light` 和 `Dark` 两套令牌。图表跟随自己所在范围的
`ThemeVariant`：

```xml
<ThemeVariantScope RequestedThemeVariant="Dark">
    <avc:CartesianChart Height="240">
        <!-- series and axes -->
    </avc:CartesianChart>
</ThemeVariantScope>
```

在代码中：

```csharp
var darkChart = new ThemeVariantScope
{
    RequestedThemeVariant = ThemeVariant.Dark,
    Child = chart,
};
```

切换 `ThemeVariantScope.RequestedThemeVariant` 会让已有的图表重新读取令牌并重绘，不需要重建图表。它适合
“浅色页面里放一张深色图”，也可以用于在运行时切换整个窗口的主题。

如果要为两种变体提供自己的颜色，在同一个资源字典的 `ThemeDictionaries` 中分别定义：

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
    </ResourceDictionary.MergedDictionaries>

    <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light">
            <Pen x:Key="Series.Stroke1" Brush="#1473E6" Thickness="1" />
            <SolidColorBrush x:Key="Series.Fill1" Color="#771473E6" />
        </ResourceDictionary>
        <ResourceDictionary x:Key="Dark">
            <Pen x:Key="Series.Stroke1" Brush="#79B8FF" Thickness="1" />
            <SolidColorBrush x:Key="Series.Fill1" Color="#7779B8FF" />
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

示例省略了其余槽位；没有覆盖的键继续使用默认主题。

## Palette 怎样分配

Palette 有十对颜色。第 `n` 对由：

```text
Series.Stroke{n}   // Pen，给线条和轮廓
Series.Fill{n}     // IBrush，给区域、柱形和标记内部
```

组成。图表按**系列声明顺序**分配槽位：

```csharp
chart.Series.Add(depth);    // 第 1 槽：Series.Stroke1 / Series.Fill1
chart.Series.Add(speed);    // 第 2 槽：Series.Stroke2 / Series.Fill2
chart.Series.Add(flow);     // 第 3 槽：Series.Stroke3 / Series.Fill3
```

规则有三条：

1. **自带颜色的系列不占槽。** 如果 `speed.Stroke` 或 `speed.Fill` 已经显式设置，`flow` 仍然拿第 2 槽。
   这样给一个系列换色不会把后面所有系列的颜色一起挪走。
2. **槽位用完就循环。** 第 11 个没有自带颜色的系列重新拿第 1 对颜色。
3. **Stroke 与 Fill 按槽位配对。** 两者可以分别设计，但同一个槽位的画刷和填充通常应是同一个色相的
   实色与淡色版本。

图表暴露了按记录取色的方法。索引从 0 开始，负数或超过十也会循环：

```csharp
var outline = chart.PaletteStroke(0);   // Pen
var fill = chart.PaletteFill(0);        // IBrush
```

图例色块和数据提示的系列色读取 `series.Color`。它返回这个系列**实际使用的颜色**：系列自己设置了颜色就取
自己的，否则取图表分配到的 Palette 颜色。

## 定义自己的 Palette

最直接的方法是在图表能看到的资源里覆盖 `Series.Stroke1..10` 和 `Series.Fill1..10`。下面从颜色列表生成
一整套十色 Palette，并只应用到一张图：

```csharp
string[] inks =
[
    "#1473E6", "#D14A8A", "#19A974", "#E58B2E", "#7657C8",
    "#3C9BD6", "#A5A72A", "#D65C5C", "#8B6F47", "#607D8B",
];

var palette = new ResourceDictionary();

for (var index = 0; index < inks.Length; index++)
{
    var color = Color.Parse(inks[index]);
    var slot = index + 1;

    palette[ChartTokens.SeriesStroke(slot)] = new Pen(new SolidColorBrush(color), 1);
    palette[ChartTokens.SeriesFill(slot)] = new SolidColorBrush(color, 0.47);
}

var chart = new CartesianChart
{
    Resources = palette,
    // axes and size...
};
```

这段代码只设置颜色，不会改掉其他轴、文字和表面令牌。它们仍会从上层主题继承。

需要让整套应用使用同一个 Palette 时，把覆盖放到 `Application.Resources`；需要让一个窗口或一张图表用
不同 Palette 时，把同样的键放到对应资源范围。默认 Palette 的大小是
`ChartTokens.PaletteSize`，目前为 10。

### 自定义 Palette 的选择建议

- 先保证相邻槽位在浅色和深色背景上都有足够对比度。
- 不要只靠颜色区分系列。图例文字、标记形状、线型和直接标签是颜色之外的第二身份通道。
- `Series.Stroke` 是 `Pen`，但 `LineSeries` 实际用 `StrokeThickness` 画线；调整线宽仍应设置系列属性，
  不要把 Palette 的 `Pen.Thickness` 当成所有系列的统一线宽。
- 一对 Stroke/Fill 应保持同一色相。Fill 的透明度通常要明显高于实色 Stroke。

## 系列自己的颜色

显式设置系列颜色具有最高优先级，并且不会被图表 Palette 覆盖：

```csharp
var depth = new LineSeries<Reading>
{
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Depth,
    Stroke = Brushes.SteelBlue,
};

var flow = new ColumnSeries<Reading>
{
    ItemsSource = readings,
    XSelector = reading => reading.When,
    YSelector = reading => reading.Flow,
    Fill = new SolidColorBrush(Color.Parse("#77D14A8A")),
    Stroke = new Pen(Brush.Parse("#D14A8A"), 1),
};
```

不同系列用来着色的属性如下：

| 系列 | 主题/显式颜色入口 |
|---|---|
| `LineSeries<T>` | `Stroke`、`StrokeThickness` |
| `AreaSeries<T>` | `Fill`、`Stroke` |
| `PlotSeries<T>` | `MarkerFill`、`MarkerStroke` |
| `ColumnSeries<T>` / `BarSeries<T>` | `Fill`、`Stroke` |
| `RangeSeries<T>` / `BoxSeries<T>` | `Fill`、`Stroke` |
| `ErrorSeries<T>` | `Fill`、`Stroke` |
| `PieSeries<T>` | 默认逐记录使用 Palette；标签用 `LabelBrush` |
| `CandlestickSeries<T>` / `HighLowCloseSeries<T>` | `RiseFill`、`RiseStroke`、`FallFill`、`FallStroke` |
| `HeatSeries<T>` | `ColorScale`，颜色表示第三个数值 |

几个需要特别记住的例外：

- **饼图按记录取色。** 它的第一个扇区从该饼图在系列列表中的槽位开始取色，扇区多于 Palette 时循环。
- **涨跌色不是 Palette 槽位。** 没有显式设置时，蜡烛和价格条从 `FinancialUp` / `FinancialDown` 取色，
  因为涨跌是读数本身的属性，不是系列在列表里的位置。
- **热力图使用 `HeatColorScale`。** 它表示一个连续值到颜色的映射，不参与普通系列 Palette。
- **只设置线宽不算自带颜色。** `StrokeThickness` 改变形状，但仍会使用 Palette 的颜色。

如果颜色来自宿主业务状态而不是固定系列身份，优先使用系列属性或自定义 `CellStyleSelector` 一类的显示
接口；如果颜色代表整个系列的身份，优先修改 Palette，让图例、提示和绘制自动保持一致。

## 改颜色还是改控件模板

只有颜色、粗细和资源值变化时，改令牌就够了。需要不同布局或结构时，才在相同的类型键下提供自己的
`ControlTheme`，例如 `ChartBase`、`DataTip`、`Legend` 或 `LegendItem`。

完整换肤并不需要继承图表控件：把默认主题和自己的资源字典合并到宿主资源中即可。先从小范围覆盖开始，
再决定是否替换整个 `ControlTheme`；这样已有系列、交互和测试都能继续工作。

---

[English version](../en/using/theming.md)
