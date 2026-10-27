# 常见问题

按症状排。每一条里"为什么"比"怎么改"重要，因为这一半的毛病会换个样子再回来。

## 图是空白的

四个原因，按出现频率排：

1. **没接主题**。图表画的每一种颜色都是一个令牌，没有主题就没有颜色。
   ```xml
   <ResourceInclude Source="avares://AvCharts.Controls/Themes/ChartTheme.axaml" />
   ```
2. **没给高度**。图表的高度由容器给；在一个不约束高度的容器（`StackPanel`、`ScrollViewer`）里它会被量成
   无限高，于是被裁、被压、或者把邻居挤走。给它 `Height`，或者放进 `Grid` 的 `*` 行。
3. **轴的种类和值的种类对不上**。把 `DateTimeOffset` 喂给 `LinearAxis`，或者把字符串喂给数值轴：
   `TryToUnit` 返回 `false`，那些点在那个维度上就是**缺失**，也就是什么都不画。这是设计如此 ——
   它不会去猜你的意图。
4. **选择器读错了**。真实案例：画廊的 ECG 页曾把整条曲线压进一列，排查到最后是页面的 `XSelector` 拿到了
   记录本身而不是它的下标 —— 值照样被画了出来，只是横坐标全都不对。**先确认横坐标读的是什么**。

## 点它、滚它、悬停都没有反应

图表**没有可绘制内容**。Avalonia 里一个没有可渲染内容的控件在命中测试中是透明的 —— 事件穿过它落到窗口的
`ContentPresenter` 上。主题给图表一个 `Background`（`SurfaceBackground` 令牌，哪怕画成 `Transparent` 也算）
正是补上这一点；用了自己的 `ControlTheme` 而没给背景，就会有这个症状。

## 事件收不到，或者收到"莫名其妙的"事件

数据点不是屏幕上的东西，背后没有 `Visual`，所以"每项事件"是图表按命中测试**自己合成的**，`sender` 是图表。
两件容易误判的事：拖拽不算点击（`Pressed`/`Released` 照报、`Click` 不报）；双击由平台给的 `ClickCount`
决定。`MouseSensitivity`（默认 5 逻辑像素）决定指针离多近才算"在这个读数上"。

## 轴标签被裁掉，或者绘图区小得可怜

- 最外端那个标签会**以刻度为中心**向外超出半个标签的宽度 —— 这半个是算进 gutter 的
  （`AxisRenderer.MeasureOverhang`），所以正常情况下不该被裁。还被裁，就查是不是外层容器在裁
  （`ClipToBounds`、固定高度、负 margin）。
- 绘图区太窄：`TickLength`、`LabelGap`、`LabelMargin`、`FontSize` 每一个都在吃地方。标签太长就
  `LabelRotation` 转过来、或者 `StaggerLabels` 错成两行；实在放不下就 `ShowLabels = false`，
  或者把间距调稀（`MajorGridPitch` 调大 = 刻度更少 = 标签更少）。
- **改这些设置会重新布局**，不只是重画：藏起标签会把那块地方还给绘图区（`AxisRenderer.Changed` 就是干这个的）。

## 标签写成了 0.6000000000000001

这是线性轴早先用默认 `G` 格式留下的毛病：0.2 累加三次就是那个样子，而 gutter 按最宽的标签量，于是一个标签
能吃掉 200px 宽的图的五分之一。现在写的是 `G9`。自己的轴覆盖了 `FormatValue` 就要自己注意这件事。

## 有些读数就是画不出来

按顺序查：

1. **值本身是缺失的**（`null`、`NaN`、空串）：这是"读数没来"，不是零。看 `NullValueMode` ——
   `Gap` 断开、`Connect` 连过去、`Zero` 画在轴给出的零点上。
2. **被 `Filter` 丢掉了**：丢掉的记录**不留槽位**，横坐标与柱的槽位都跟着收缩。
3. **值的种类那根轴不认识**（见第一条）。
4. **被 `MaxReadings` 丢掉了**：有界窗口只留最新的若干条。

## 柱挤在一起、宽得离谱、或者第一根被切成一半

- 带宽（一个槽位多宽）是从读数自己的间距算出来的，`ColumnWidthRatio`（默认 0.65）与 `MaxColumnWidth`
  两个旋钮管"占多少"。
- **只有带宽系列参与布局**：折线不占位，所以"柱 + 线"里的柱站满整个槽位（`SeriesLayout.Overlay`）。
  两组柱并排要的是 `SeriesLayout.Cluster`。
- 首尾的柱不该被切成一半：柱系列会为每个槽位声明半格端部余量，这一条在测试里钉着。若你看到的是别的东西
  （比如两个系列的颜色错位），先查 `Visible` 与调色板槽位。
- 分类轴的槽位**按类别首次出现的顺序发放**；数据顺序变了而槽位没变，就调 `ClearCategories()` 重新来过。

## 第二把尺不见了

- **每个维度只画一根额外轴**。第三根不会凭空有个位置：用它的系列照旧被它摆放，只是没有自己的标签轴。
- 额外轴画在**远边**（上/右），且不长网格 —— 这是有意的。
- 带来那根轴的系列被隐藏（`Visible = false`）时，轴也跟着收走。

## 数据变了，图没变

| 你做了什么 | 要做什么 |
|---|---|
| 改了一个普通 `List<T>` | `series.InvalidateData()` |
| 在中间插入/删除，或 `Clear` | 会整体重读（前提是集合会广播变化） |
| 换了 `ItemsSource` 里的元素但集合没广播 | `InvalidateData()` |
| 改了记录里的属性 | 记录实现 `INotifyPropertyChanged`，否则 `InvalidateData()` |
| 在别的线程收数据 | `Push`（见[性能](performance.md#线程)） |

## `Push` 抛异常

消息是"只有当 `ItemsSource` 是一个可以添加的集合时才能推送"。原因见[数据](data.md#数据从别的线程来)：
推入的记录在应用时会**进入 `ItemsSource`**，所以源必须可加、不能只读。数组是 `ICollection<T>` 但会拒绝添加，
所以它在 `Push` 的那一刻就报错，而不是把记录悄悄丢掉。

## 导出的图片糊，或者只看见一个角

`RenderImage` / `SaveImage` 是按你给的尺寸**重新布局**，不是把屏幕上的位图放大。所以：

- 要更多细节就给更大的 `PixelSize`，或者给 `scaling`（两者都影响细节，`scaling` 还决定位图的
  dots-per-inch）。
- 只看见一个角，是因为 `scaling` 大于 1 时位图的 dpi 不是 96，而 Avalonia 的 `Image` 会按**像素尺寸**画它
  （`Stretch` 的三种取值都一样）。预览要自己缩放着画，画廊 Export 页有现成写法。

## 主题改了，屏幕上的图表没变

- 图表跟着**自己所在的资源范围**的变体走：想要一张深色图放在浅色页面上，用一个 `ThemeVariantScope`。
- `ChartDefaults.Configure` 是**启动时**的调用：已经在屏幕上的图表保持它被装扮时的样子，直到它自己的资源
  变了 —— 和别的主题一个规则。
- 令牌覆盖要放在图表能查到的范围里（最外层的应用资源、窗口资源，或 `ChartDefaults.Current.Theme`）。

## 金图测试失败

金图（把页面渲染成位图与基准 PNG 比对）只在 `AVCHARTS_UPDATE_GOLDENS=1` 时才写回，所以一次失败的构建
永远不会悄悄改掉比对基准 —— 换图是一个**有人看过的决定**。录完必须逐张看过。

## Flex 里有、这里没有的东西

| Flex | 这里 |
|---|---|
| 动画与效果（`SeriesSlide`、`SeriesZoom`、`SeriesInterpolate`、`showDataEffect`） | v1 不做动画；承诺的是"每帧只重录受影响的图层"。要动画就是宿主按帧改数据或窗口 |
| 系列上的数据标签 | 只有饼图自带标签（`LabelSelector`）；其它系列的值标注靠数据提示，或者写一个图表元素 |
| CSS 类选择器 | 令牌 + XAML 样式 + `ControlTheme`（见[主题与调色板](theming.md)） |
| `PolarChart` / 极坐标轴 | `PolarTransform` + `PieChart` / `PieSeries<T>` |
| 拖放 `ChartItem` | 没有；用 `ItemPointerPressed` / `Released` 加宿主自己的拖放 |
| `minField`（悬空柱） | 用堆叠（`IStackable`），或者把这一段画成 `RangeSeries<T>` |
| 每点一种渲染器类 | 标记是 `IMarkerRenderer` 接口；画法改在系列子类里 |

## 想更进一步

这张库的规矩是"用证据说话"：每一个结论在
[设计简报](https://github.com/slobber/av-charts/blob/master/docs/design-brief.md)（决策与契约）与
[落地日志](https://github.com/slobber/av-charts/blob/master/docs/design-log.md)（怎么量的、改了几次、
测试数）里都有出处。遇到文档和实现对不上的地方，先查 API 注释（每个公共成员都有，而且写了为什么），
再查这两份文档。

---

[English version](../en/using/troubleshooting.md)
