# AvCharts 设计共识（决策记录）

> 状态：**v1 完成并发布（0.1.1 在 NuGet.org），v1.1 收尾中**（2026-10-16 整理）。本文记录已达成的决定与理由、**已经完成的功能**（§13）与**未决项与待办**（§14）。
> 过程记录另放一册：**`docs/design-log.md`** —— 每一次改动"怎么量、量到多少、踩了什么坑、当时否掉了什么"按时间记在那里；本文只留结论。
> 语言约定：本文件用中文（内部决策记录）；**代码注释、XML 文档、README、NuGet 描述、样例说明一律英文**。
> 事实纪律：文中标注"实测"的结论都来自本机可查的证据（Apache Flex 源码、Avalonia 12.1 包、Gitea API、LiveCharts 文档站），不凭印象。

## 0. 一句话定位

C#/Avalonia 12 版的 `mx.charts`：继承 Flex 图表的**架构与类型名**，成员命名与工程实践 C#/Avalonia 化；默认配色**不复用 Halo**，以 SciChart.js light 主题为灵感重新调色，并**分别**为 light/dark 调色。

## 1. 项目形态与命名

- 目标是**公开库**（发 NuGet、要 API 稳定性承诺），按个人库的节奏交付：发布工程是 v1 收口时才做起来的（现在已经做好 —— 0.1.1 在 NuGet.org，见 §12）。
- 命名策略 **B 档**：类型名逐字继承（`ChartBase` / `CartesianChart` / `LineSeries` / `LinearAxis` / `AxisRenderer` / `ChartItem` / `DataTransform` / `ChartState` …），成员名 C# 化（PascalCase、枚举代替字符串常量、`[Flags]` 代替 uint 位掩码、`double?` 代替 `NaN` 哨兵）。**两处按用户要求偏离**：`AxisTickPlacement`（原名与 Avalonia 的类型撞名）与 **`SeriesBase`**（原名 `Series` 与它的命名空间 `AvCharts.Series` 撞名 —— 一个命名空间与它里面的类型同名时，`using AvCharts.Series;` 之后写 `Series` 会被解析成命名空间，宿主连 S1 的 `class Mine : Series<T>` 都写不出来；`ChartBase` / `SeriesBase` 这一对也更对称）。**这是破坏性变更**：0.1.x 里用 `Series` 的地方都改成 `SeriesBase`（下一版应当是 0.2.0）。
- 命名空间：根 `AvCharts`；模型按文件夹分六个（`AvCharts.Axes` / `.Series` / `.Data` / `.Drawing` / `.Transforms` / `.Elements`）；控件 `AvCharts.Controls`；主题 `AvCharts.Themes`。
- 程序集边界（**已冻结**：XAML 的 `clr-namespace:…;assembly=…` 映射使搬类型成为破坏性变更）：

```
src/AvCharts/          命名空间 AvCharts（模型按文件夹分六个：见下表）   轴、series、值、绘制内核、变换、元素
src/AvCharts.Controls/ 命名空间 AvCharts.Controls / AvCharts.Themes   ChartBase:Control、DataTip、Legend、ControlTheme、主题资源
src/AvCharts.Demo/     画廊（门面 + 金图视觉资产来源），net10.0
src/AvCharts.Tests/    单测 + 契约测试 + 金图测试（跟房子模板一样放在 src/ 下）
```
`src/AvCharts/` 里**按功能分文件夹，命名空间跟着文件夹走**（`Axes/` → `AvCharts.Axes` 这种，一个文件夹一个命名空间 —— 2026-10-16 按用户要求改，此前是唯一一个 `AvCharts.Charts`；XAML 那边每个命名空间各有一条 `XmlnsDefinition`，`avc:` 前缀对宿主不变）。**同日把中间那层 `Charts/` 去掉**：命名空间按**叶文件夹**取名之后，路径里那个 `Charts` 什么都不代表（既不是命名空间的一部分，也不是程序集名 —— 程序集与根命名空间都是 `AvCharts`），六个文件夹直接躺在工程根下，与 `src/AvCharts.Controls/Themes/` → `AvCharts.Themes` 同一种做法：

| 文件夹 / 命名空间 | 装什么 |
|---|---|
| `Axes/` → `AvCharts.Axes` | 轴的协议与基类（`IAxis` / `AxisBase`）、`AxisRenderer`、轴图层、刻度与梯级（`AxisStep` / `ClockLadder`）、五种轴（Linear / Category / DateTime / Log / TimeSpan）、轴上的段（`AxisSection`） |
| `Series/` → `AvCharts.Series` | `SeriesBase`（系列基类，原 `Series`）与它的一族（`CartesianSeries` / `BandedSeries` / `StatisticalSeries` / `MarkupSeries`）、全部具体系列（线面族 / 带宽族 / 金融 / 统计 / 饼）、带宽布局（`BandLayout`）、采样（`Sampling`）、标记渲染器、路径选择器（`PropertyPath`）、`SeriesRenderContext` |
| `Drawing/` → `AvCharts.Drawing` | `ChartLayer` 与五种图层、批量几何 `GeometryBatch` |
| `Data/` → `AvCharts.Data` | `ChartValue`、`ChartDimension`、`DataDescription`、`ChartItem`、`HitData` |
| `Transforms/` → `AvCharts.Transforms` | `DataTransform`、`PolarTransform` |
| `Elements/` → `AvCharts.Elements` | `ChartElement` / `IChartElement`、`Section` |

- 硬约束：`AvCharts` **绝不依赖 `Avalonia.Controls`** —— 这层拆分的全部意义。
- **落地修正**：Avalonia 12 **没有** 独立的 `Avalonia.Base` / `Avalonia.Controls` / `Avalonia.Markup.Xaml` 包（前两个在 nuget.org 上都是 404），三者在同一个 `Avalonia` 元包里。所以这条约束无法靠包引用表达，改由**测试**守住：`AssemblyBoundaryTests` 断言 `AvCharts.dll` 的引用程序集里不含 `Avalonia.Controls` / `Avalonia.Markup.Xaml`。约束不变，执行方式变了。
- 类型命名空间即层：根 `AvCharts` 加它底下的六个模型命名空间 / `AvCharts.Controls`（控件）/ `AvCharts.Themes`（主题）。
- 将来若要做"真中立核心"（OxyPlot / LiveCharts 式），入口设在 `AvCharts` 内部已有的绘制后端 seam 上，**不新增公共抽象**。

## 2. 渲染架构（决策 C）

- 图表是自定义 `Control`，内部固定若干 layer 视觉：背景元素 / 网格线 / series / 标注 / 轴 / DataTip / Legend。
- `Series`、`ChartItem`、`Axis`、`AxisRenderer` 都是**模型对象，不是 Visual**；命中测试由我们自算（Flex 本来也是自算的：`DataTransform.invertTransform` + `findDataPoints`）。
- 每个 layer 只做批量绘制（同层几何合并），悬停/选中只在受影响的 layer 上 `InvalidateVisual`。
- series 层内部再拆**基础 bucket**（批量几何，数据/轴/主题变化才重录）与**交互 overlay**（只画高亮项），使悬停成本是 O(高亮点数) 而非 O(N)。
- 例外：**DataTip 与 Legend 是真控件**（吃到 ControlTheme、可换皮、可用模板）。
- 空心/半透明标记的悬停覆盖会有重影：悬停项变化时把该点从基础 bucket 排除并重录一次基础层（人类手速下 10k 点约 1ms），动画帧只重录 overlay。
- 补丁：**S4 标注元素可以声明为"承载 Visual"**，由 chart 在 overlay 层挂少量真控件（LiveCharts 的 VisualElements 模式）。不是"每个数据点一个控件"——**这一半 2026-10-17 做了**：`ChartElement.Visual`（`Layoutable?`）+ `ChartElement.VisualAnchor`（`Point?`，控件的**中点**落在这里），图表把控件挂进自己的视觉树（图层之上、tip 之下）、量它、按锚点摆它、元素走时把它摘掉；`VisualAnchor` 给 null 就整块隐掉（"不在这儿"而不是"摆到角落"）。每一趟布局都重读一次，所以元素换一个控件、或读数出了视野都不用先摘再挂 ✓。

## 3. 数据管道与数据模型

- **泛型为主**：`Series<T>` + `Func<T, ChartValue>` 选择器（编译期检查、结构体选择器零装箱）。`IEnumerable<T>` 协变，所以 `LineSeries<object>` 天然支持 `DataTable` / `IDictionary` / 动态数据。**不做第二套 object 流水线。**
- XAML 路径：非泛型子类 `LineSeries : LineSeries<object>`，取值走"路径字符串 → 编译好的访问器"，与泛型路径共用同一个取值内核。
- `ChartValue` 是结构体，种类含 `Empty`（缺失）/ Number / DateTime / Category，并有 `double?`、`DateTime?`、`string?` 的隐式转换。
- 管道分期：**Normalize → Filter → Reduce(采样) → Render/HitTest**。归一化后的值数组是唯一事实来源，之后不再触碰用户对象（装箱只发生一次，不是每帧）。
- 增量更新：`INotifyCollectionChanged` + item 的 `INotifyPropertyChanged`；不订阅则退化为全量重读。
- **线程模型（v1 明确）**：数据变更只在 UI 线程；提供"后台推送 + UI 线程按帧合批应用"的入口，避免每点一次 invalidate。工控机目标是这条约束的来源。
- **`ChartItem` 惰性物化**：值数组是存储，`ChartItem` 只为"可见 + 采样后"的部分物化，`Series.Items` 是**惰性视图**（文档写明）。
- `Filter`（`Func<T,bool>`）= 丢弃整条记录；空值 = 记录有效但某维度缺失。**两者语义不同，文档写死。**

## 4. 空值（Null Points）——v1 纳入

两边事实：LiveCharts 是**断线**（`double?[]`、`ObservablePoint?[]`，示例名就叫 Gaps/Null Points）；Flex 是**可配置丢弃**（`filterData` + `filterDataValues="nulls"` → `isNaN` 的点 `continue` 跳过），且 `AreaSeries.findClosestValidValue()` 会对缺失值做**就近填充**（还加 `0.01*Δidx` 的微小偏移）—— 那是编造数据，不继承。

- 每系列 `NullValueMode { Gap, Connect, Zero }`，**默认 `Gap`**（如实显示断口优先于视觉顺滑）。
- 柱/条：空值不画柱但**保留槽位**（否则后续柱整体位移、类别轴错位）。饼图：空值不进扇区也不进百分比分母。
- 值域：空值不参与；某轴某维度所有系列全空 → 轴退化为零长 → **回落默认值域**（需专门测试）。
- 采样：**先判空、后分桶**，且桶区间照常推进（不能先把空值删掉再分桶，否则值轴点位整体位移）。桶内全空 → 断口；部分空 → 对非空取 min/max。
- 命中测试与 data tip 跳过空值；十字线吸到最近**有效**点。堆叠里空值视觉高度为 0 但保留"缺失"标记，tooltip 能区分"0"与"无数据"。

## 5. 采样（Sampling）——v1 纳入

- `Series.Sampling`，`SamplingMode { None, Lttb, MinMax, Average }`，**归 series 级**（怎么砍取决于标记怎么画：折线要 min/max 包络否则尖峰消失；散点要网格抽稀；堆叠要按簇处理）。chart 级只留一个总开关，用于精确出图/调试。
- **点数 ≤ 可见像素列数时采样自动不生效**（精确绘制）：采样是"数据比像素多"时的降级手段，不是常开状态。
- v1 交付 `None` / `MinMax` / `Lttb` / `Average`（接口只有被语义不同的实现压过才定得对）。
- Flex 侧对照：**完全没有**采样（`downsampl` / `decimat` / `cull` 全 0 命中）。

## 6. 轴与尺度

- 轴类型：`LinearAxis`、`CategoryAxis`、`DateTimeAxis`、`LogAxis`（共用 `NumericAxisBase`）v1 全上；另加 `TimeSpanAxis`（时长轴，Flex 没有，成本低）。
- `AxisScaleMode { Fit, Fixed }`：`Fit` = Flex 行为（把 `[min,max]` 撑满绘图区，于是"每点多少像素"成了控件尺寸的函数，网格长宽比随尺寸漂移）；`Fixed` = **像素/数据单位恒定**，可见值域由绘图区尺寸推出。
- `AxisBase.PixelsPerUnit`（unit 按轴类型定义：Linear = 1 数据值、DateTime = 1ms、Log = 1 数量级、Category = 1 槽位）。**"一单位算不算一格"由柱系列的带宽回答**：分类轴上一单位 = 一槽（它的单位空间就是槽位本身），其余轴上取**读数自己的间距** —— 没有一个替所有轴回答"一单位多大算一格"的派生量。
- Fixed 模式三条语义：**`Minimum` 是视窗锚点、原样采用（不吸附到格距）、`Maximum` 是派生量**（设置被忽略 + Debug 警告）；**网格线落在 `Interval` 的整数倍上并铺满绘图区、边缘允许残缺末格**（不量化绘图区，否则图表不填满容器）；分类轴多出的空槽位只画网格不画标签。固定尺度**连续平移**、网格随数据滑动（用户拍板，见 `docs/design-log.md` 2026-10-14）。
- **分类轴在 `Fixed` 模式下的取舍**：`Fixed` 承诺的是"像素/单位恒定"，而分类轴的单位是**槽位**、槽位只能落在整数上 —— 所以恒定的只有"整槽"这一档，**两个槽之间不发明中间刻度**（发明出来的刻度没有类别可写，写数字则是另一种错）。`Interval` 给非整数时按整槽取整；空槽位只画网格不画标签（上一条）。
- 刻度：`Interval` 为 `double?`，**null = 自动吸附**（等格默认行为：由 `MajorGridPitch` 与 `PixelsPerUnit` 反推并吸附到漂亮档位），显式给值则固定刻度。`MajorGridPitch` 默认 60px、`MinorGridPitch` 默认 12px（5:1，沿用图纸习惯）。
- 轴标签的避让：**做了，而且是无条件的**（Flex 对应 `canDropLabels`，默认还看轴自己的 `preferDropLabels()`；我们没有开关）—— 从近端逐条贪心保留、放不下就丢，**末标签若塞得下一定保住**（轴的两端是眼睛最先找的地方）；`MeasureGutter`/`MeasureOverhang` 按**全部**标签量，免得"少画几个标签 → gutter 变窄 → 再少画几个"来回抖。
- **轴上的四件装饰已经全取**（Flex `AxisRenderer` 里声明过的那四件，见 §13 的功能表）：里侧/交叉刻度（`AxisTickPlacement`）、标签旋转（`AxisRenderer.LabelRotation`）、错层（`AxisRenderer.StaggerLabels`）、轴标题（`IAxis.Title` + `AxisRenderer.TitleGap` / `TitleDirection`，令牌 `Axis.Title.Foreground` 从轴标题那一步起真的被消费）。**四件之外，Flex 的次刻度也补齐了**（`minorTickLength` / `minorTickPlacement` / `minorTickStroke` → `AxisRenderer.MinorTickLength` / `MinorTickPlacement` + 令牌 `Axis.Tick.Minor`）：`MinorGridPitch` 一向只画**次网格线**，现在轴上的**次刻度**也画得出，且**不占新的房间**——它们落在标签已经要来的 gutter 里。默认长度 0（不画），所以既有的图一张未变。
- 诚实说明：**"格子像素严格恒定"数学上做不到**（除非放弃漂亮刻度值）—— 实际会在目标值附近按档摆动（1/2/5 梯级约 ±60%，细化到 1/2/2.5/5 约 ±33%）。**分档 zoom**（吸附到预设档位）推 v1.1。
- 梯级按轴类型：数值轴 1/2/2.5/5 十进制；时间轴必须日历感知（ms/s/min/h/day/month…）；对数轴按数量级。梯级策略可被自定义轴覆盖，但**不作为 v1 公开配置**。
- `Axis.Separators`（显式刻度值列表，LiveCharts 有、Flex 无）纳入 v1。
- **多图共用一根轴时怎么对齐**：一个轴实例服务多张图是 v1 的既有性质；**刻度要真的对得齐**，还得两张图的绘图区落在同一列 —— 而每张图给纵轴留的房间是它自己标签的宽度（价格 `110` 与成交量 `2,000` 不一样宽）。所以 `AxisRenderer.MinimumGutter`（double，默认 0）：**这一根轴至少要留多少房间，无论标签需要多少**。宿主把几张图要来的房间取最大、各自按差值设置，绘图区就落在同一列（`Panes` 页就是这两行算术，加一个把两种状态摆出来的旋钮 ✓）。
- 网格线**吸附设备像素**（只取整位置、不取整间距）—— 波形比例优先，网格锐利靠吸附。
- 自定义轴契约（S2）：值↔单位变换、刻度生成、标签格式化、`DescribeData` 声明的字段需求 —— 心电那种 0.04s / 0.2s 非十进制梯级靠它。

## 7. 主题

- 机制：`ControlTheme` + `ThemeDictionaries`（Light/Dark 两套），资源键是**公共 API** 并冻结；图表订阅 `ActualThemeVariantChanged` 做重绘（不重建）。宿主用 `Resources` 覆盖任意令牌即可（一张图单独换皮不需要新 API）。
- 令牌全集（**类型已定**：填充类 `IBrush`，描边类 `Pen` —— 渐变与虚线因此免费，对应 LiveCharts 的 "Paints"）：

| 组 | 令牌 |
|---|---|
| 表面 | `Surface.Background`、`Surface.Border`、`Surface.FocusRing` |
| 轴带 | `Axis.Band.Fill`、`Axis.Band.FillAlt` |
| 轴 | `Axis.Line`、`Axis.Tick`、`Axis.Tick.Minor`、`Axis.Label.Foreground`、`Axis.Title.Foreground` |
| 网格 | `Axis.GridLine.Major`、`Axis.GridLine.Minor`、`Axis.GridLine.Origin` |
| 数据 | `Series.Stroke1..N`（`Pen`）、`Series.Fill1..N`（`IBrush`） |
| 标记 | `Marker.Fill`、`Marker.Stroke`、`Marker.Hover.*`、`Marker.Selected.*` |
| 十字线 | `Crosshair.Line`、`Crosshair.Label.Background`、`Crosshair.Label.Foreground` |
| 图例 | `Legend.Background`、`Legend.Border`、`Legend.Foreground`、`Legend.Item.Disabled` |
| 提示 | `DataTip.Background`、`DataTip.Border`、`DataTip.Foreground`、`DataTip.Shadow` |
| 金融 | `Financial.Up`、`Financial.Down` |
| 选择区 | `Selection.RubberBand.Fill`、`Selection.RubberBand.Stroke` |
| 滚动条 | `Scrollbar.Track`、`Scrollbar.Thumb`、`Scrollbar.Thumb.Hover` |
| 概览 | `Overview.Background`、`Overview.Window.Fill`、`Overview.Window.Stroke` |

`Axis.GridLine.Origin` 单列的理由：Flex 的 `GridLines` 专门有 `horizontalShowOrigin` / `verticalShowOrigin`（轴原点线，通常更粗更深，即工程图里的零线），SciChart 只是拿 `gridBorderBrush` 糊过去 —— 它必须能单独控制。

- 分配语义：chart 持有有序调色板，按 series **声明顺序**取槽；legend 与 data tip 同源；`PieSeries` 按数据点索引取色；**显式设了 Fill/Stroke 的 series 不占槽位**（修掉 Flex 里"给首个系列设色导致后续全体错位"的坏味道）。
- 色板（**已定稿**，就是下面这些值；对照图与检查脚本是 `tools/palette-preview.py` 与 `tools/palette-vision.py`）：前 5 个 light 描边沿用 SciChart.js light 主题，按同风格扩到 10 个（5 色对饼图/多系列不够用）；**dark 单独调一组**（提亮、降饱和）—— SciChart 自己在 dark 主题里也改了色板，同一组颜色跨明暗不成立。
  - light 描边：`#F48420` `#AE408E` `#30BC9A` `#209FD9` `#264B93` `#7A9A2E` `#C25E5E` `#7C6BB8` `#B08A3E` `#5E7387`
  - dark 描边：`#FF9E45` `#D06BAE` `#45D6B2` `#4CB6E8` `#6C8FD8` `#A8C05C` `#E08080` `#9E90D8` `#D8B45E` `#8C9CAC`
  - light 中性色沿用 SciChart：surface `#F9F9F9`、刻度字 `#333333`、主/次网格 `#CFCFCF`(+47%)、轴带 `#AAAAAA`@3.5%、轴标题 `#777777`；dark surface `#1C1C1E`。（`#264B93` 深蓝在深底上几乎不可见，dark 的第 5 色因此提亮成 `#6C8FD8`。）
- 由对照图得到的结论：**折线默认不填充** —— 47% 的 fill 只服务面积图与柱/条。
- 色觉可分辨性检查**已做**（`tools/palette-vision.py`：deuteranopia 全强度模拟 + CIELAB CIE76 两两距离）：**相邻槽位在模拟下都还分得开**（light 最近一对 24.7 dE、dark 11.1），但**非相邻两两会塌**（light 2↔10 只有 1.6 dE、6↔9 是 3.0）—— 这是**十色板与红绿色盲的算术**，任何十色板都做不到两两可分（能稳妥分开的上限是五、六支）。**用户拍板：不为此增加虚线/形状这第二条身份通道**；真要做时那条量法仍是起点。
- 两条已经落地的默认：**标记的默认形状是圆形**（`Marker = null` 仍然表示"什么都不画"；填充是系列色**淡一档**、边框是同一个颜色**实心一档**，与 §13 的 `Markers` 一致）；主题的两个金融 token `Financial.Up` / `Financial.Down` 由蜡烛与开收刻线系列消费（系列自己设了颜色就用自己的）。
- 与房子设计系统的关系：`iPaas.Controls` 已有 `Chart1..5`（**同一色相的 5 个明度档** `#8EC5FF`→`#193CB8`，light/dark 相同），适合分档、不适合区分系列。av-charts 自带多色相默认板，接入宿主时用资源键覆盖即可，**不内建单色 ramp 机制**。

## 8. 交互（v1 范围）

- 命中测试：`FindDataPoints(Point)` / `GetAllDataPoints()` / `GetItemsInRegion(Rect)` / `MouseSensitivity`（默认 5px，Flex 同名同默认），`HitData` 带 `Distance` / `Item` / `Element` / `ChartItem` / `X` / `Y` / `SeriesColor` / `DisplayText`。带空间索引。
- **命中测试问的是"你画了什么"，不只是"读数在哪"**：柱与条按带距命中；折线按**线段**、台阶按**横档 + 立档**、面积按**线段 ⋃ 它填的那块区域**（`CartesianSeries.Between(previous, current, point)`，默认"只在读数上落墨"的系列给无穷远）；**指针正压在某个读数上时就是那个读数**（半个像素内直接短接，免得别人的线段抢走它）。
- 事件：`ItemClick`、`ItemDoubleClick`、`ItemPointerPressed/Moved/Released/Entered/Exited`、`SelectionChanged`（`ChartItemEventArgs`）；另提供 **`ICommand` 属性**（MVVM，Avalonia 用户主力）。文档要写明：数据点不是 Visual，**这些"每项事件"是 chart 在命中测试结果变化时合成的**。
- 选择：`SelectionMode { None, Single, Multiple }` + 完整键盘语义（方向键移动、Space 选择、**Ctrl 只移动 caret 不改选择、Shift 从锚点区间选择**）。需要 `Focusable` 与焦点视觉令牌。
- DataTip：`DataTipMode { Item, Mouse, None }` + `ShowAllDataTips` + 模板/内容定制 + **`TipStack` 防重叠堆叠**（LiveCharts 没有，我们要比它好）。
- 十字线：**v1 纳入**（专业场景标配，成本低，正好用 overlay 层）。默认 **crosshair 关、data tip 开**。
- 图例：点击切换系列可见性 **v1 纳入**（Flex 没有：`Series` 连 `visible` 属性都没有），需要 `Series.Visible` + `Legend.Item.Disabled`，且隐藏系列不参与轴值域与布局；位置四个方位（`LegendPlacement`），上下时 item 横排、左右时竖排，朝向图表那一侧留 8px 间隙。
- pan / zoom：滚轮 = 双轴同步缩放（锚在指针）；拖拽 = 平移；`Shift` = 仅横轴、`Ctrl` = 仅纵轴、`Alt` + 拖拽 = 框选（见下一条）。**Fixed 模式 zoom 写 `PixelsPerUnit`、pan 写 `Minimum`；Fit 模式两者都写进 `Minimum`/`Maximum`** —— 两种模式都退化成单纯的属性写入。
- **窗口的三件底层入口 + 一条滚动条**：`PanBy` / `ZoomBy(factor, anchor)`（鼠标、滚轮、键盘、滚动条、外部代码共用一条路径）、图表级 `DataToPoint(...)` / `PointToData(Point)`、`VisibleRangeChanged` 事件（两张图联动靠它，`Two scales` 页就是现场）；控件是 `ChartScrollBar`（`AvCharts.Controls`）：横竖两条、拖滑块平移窗口、点轨道把窗口挪到指针处、滑块长度 = 可见份额、位置 = 窗口在读数里的位置、`MinThumbLength` 给个下限、只读 `CanScroll` 让宿主藏掉一条没得滚的条。**两条规矩按用户要求**：默认**细**（宿主没给厚度时 8 逻辑像素）、**两端没有箭头按钮**（轨道 + 滑块就是全部）。轨道画在**绘图区自己那一段**上（bar 跟着 chart 的 gutter 对齐，见 `ChartScrollBar.Track`）。**概览条也做了**：`ChartOverview`（同族控件）把**整段读数画成一条线**（每条系列一条，用图表给它的颜色；MinMax 抽稀到像素列，空值断开），窗口是压在它上面的一个矩形 —— 可拖中间（平移，走 `PanBy`）、可拖**两端**（缩放，走 `ZoomBy`，正是滚动条做不到的那半），点别处把窗口挪过去。**轨道与滚动条同一条规矩**（绘图区自己那一段），所以两条叠在一起时窗口的左右边与滑块、与绘图区的边一一对齐（`Scrolling` 页一眼可见）。
- 多图联动**不需要新机制**：实测 Flex `AxisBase.registerDataTransform(transform, dimension)` 把 transform 推进数组、`describeData` 对全部注册者聚合 → **一个轴实例可服务多个图表**。v1 写测试锁住这个性质（Two scales 页就是一条轴配两条系列）。
- **框选区间**（Flex `RangeSelector`：`chartClasses/ChartBase.as` 里的私有 helper，画 `#DAF1FF`@50% + `#009DFF`，数据区里按下时创建）**做了**，手势按用户拍板用 **`Alt` + 拖拽**：Shift / Ctrl 已经占了"单轴平移"，Alt 是唯一空着的修饰键。规则：在**绘图区内**按下（gutter 里按下不算，那儿的带子会圈住"标签底下的读数"）、拖动时画出带子、抬起时把框内的读数全部选中（`GetItemsInRegion`，没有就放开原有选择）；**只在 `SelectionMode = Multiple` 下提供** —— 框选要的是"一叠读数"，模式说不能存一叠时就不该画那条带子。带子画在 `SelectionLayer` 上（选中环之下），穿 `Selection.RubberBand.Fill` / `.Stroke` 两个令牌（值早在 4.1 就定好、一直没人用），`ChartBase` 侧是 `RubberBandFill` / `RubberBandStroke` 两个属性 + `BandStart` / `BandMove` / `BandEnd` 三个入口（与 `DragStart` 同形）。拖拽改数据**不做**。

## 9. 扩展契约（v1 冻结形状）

- **S1 自定义系列**：继承 `Series<T>`，重写 `UpdateData → DescribeData → BuildGeometry → Draw`；提供 `SeriesRenderContext`（`PlotArea` / `ValueToPoint` / `Clip`）；必须能重写 `HitTestItem(Point) → HitData?`（默认实现 = 值位置 + `MouseSensitivity` 半径）。
- **S2 自定义轴**：继承 `AxisBase`（见 §6）。
- **S3 自定义标记**：`IMarkerRenderer`，**接口组合**不走继承；内置**五个点状形状** `Markers.Circle` / `Square` / `Diamond` / `Cross` / `Triangle`（即 Flex 的 Circle / Box / Diamond / Cross / Triangle）；要别的一律用 `GeometryBatch.AddPolyline` 走接口。Flex `renderers/` 里其余那些不在这一格，各自有归属：`AreaRenderer` / `LineRenderer` / `ShadowLineRenderer` 是"线怎么画"，在我们的架构里就是系列的 `BuildGeometry`（没有单独的 renderer 类）；`HLOCItemRenderer` / `CandlestickItemRenderer` / `WedgeItemRenderer` 随金融与饼图一起押后（§10）；`ShadowBoxItemRenderer` 是"方框 + 影子"，在 Avalonia 里是形状与主题两件事，不并成一个形状。
- **S4 自定义图层元素**：非 series 的元素基类 + `CartesianDataCanvas` 的现代对应物（阈值线、参考区、事件标记）；v1 文档标 **experimental**；可承载 Visual（见 §2 补丁）。**（2026-10-16 更新）元素基类 `ChartElement` 与"宿主自己写元素"这条路早已就位 ✓；"参考区"那一格现在由库自己给：`Section`（图上的一段）与 `AxisSection`（轴上的一段，由 `AxisRenderer.Sections` 持有 ✓）；"可承载 Visual"那一半 2026-10-17 做了 ✓：`ChartElement.Visual` / `VisualAnchor` 两个虚属性，图表把宿主那件真控件挂在图层之上、按锚点摆好（`Annotations` 页第二例就是一枚带按钮的注记 ✓）；**顺带修掉一处真缺口**：`DataTransform.AddElement` 原来只给 series 设 `Transform`，普通元素的 `Transform` 一直是 null（而属性注释写着"注册时设置"）✗。**
- **钩子顺序是公开契约**（Flex 把三阶段当契约文档化），配契约测试断言顺序。
- 主干用继承 + `protected virtual`；**能力用接口**（堆叠走 `IStackable`，与 Flex 一致）。
- `DataTransform` 公开可注入（标 advanced）—— 自定义坐标系的唯一入口，后续饼图 / 能力六边图（雷达）靠它。

## 10. v1 覆盖矩阵

| 族 | v1 | v1.1 | v2 / 不做 |
|---|---|---|---|
| 点线面 | Line / Area / Plot / Bubble + StepLine / StepArea | | |
| 带宽 | Column / Bar（cluster / overlay / stacked） | | |
| 轴 | Linear / Category / DateTime / Log / TimeSpan | 标签旋转 / 错层 / 里侧刻度 / 轴标题 / 次刻度 ✓ | |
| 饼图 | | PieChart（PolarTransform）✓ | |
| 金融 | | Candlestick / HighLowClose ✓ | |
| 统计 | | Box / Error / Range（置信区间）✓ | |
| 标注 | | Sections / Axis Sections ✓ | 自由绘制层（CartesianDataCanvas） |
| 热力 | | | Heat series |
| 极坐标 | | 南丁格尔玫瑰 / 嵌套饼 ✓ | Gauge / 太阳图 |
| 共享轴 | 共享轴实例（配测试）✓ | 多图共享轴 UI、刻度对齐 | |
| 导出 | | 图表导出图片 ✓ | |
| 动画 | 不做（只留"每帧只重录受影响 layer"的骨架） | SeriesInterpolate / Slide / Zoom、分档 zoom | |
| 其他 | 十字线、图例切换可见性、`ICommand` | 全局默认 ✓（落地名 `ChartDefaults`）、滚动条与概览 ✓（`ChartScrollBar` / `ChartOverview`）、框选区间 ✓（`Alt` + 拖拽） | 拖拽改数据、地图、轴分页 |

**已完成的功能见 §13；表里还没做的那几件见 §14。**

Flex 的图表类型类（`LineChart` / `ColumnChart` / … 共 6 个）**不移植**（理由来自源码）：`LineChart.as` 的正文只有一趟 `initStyles()`（给 `mx.charts.LineChart` 那个选择器造出 `haloLineSeries0..N` 一串 Halo 样式），**没有一行行为**；而 `ColumnChart` 真正做的那件事（`type` 的 cluster / overlay / stacked、`columnWidthRatio` 按条数分摊与偏移）在我们的架构里是**图表自己的布局**（`SeriesLayout` + `BandLayout`，`CartesianChart` 已经拥有它）。这些类在 Avalonia 这边只会是"一个名字，加上照样由你自己添加系列"，属于文档而不是类型；"迁移锚点"的角色由本表与 `CartesianChart` 的注释承担。`ChartBase.type` 按 B 档改名 `SeriesLayout` 枚举这一条不变。

**图例的排布引擎也不是我们写的**：`Legend : ItemsControl`，摆在哪里由宿主决定、怎么排由宿主的 `ItemsPanel` 与 `ControlTheme` 决定；Flex 的 `Legend.as`（6,721 行、140 个方法）体量全在"图例是一个自绘容器"上 —— 自己测量、自己排、自己滚动、自己处理鼠标 —— 而那个容器在我们这边是宿主的页面。

**v1 功能验收**：Column + Line 组合图、双 Y 轴、时间轴、堆叠柱、十万点散点、light/dark 切换、data tip、十字线、图例切换、ECG（Fixed 尺度 + 等格 + 采样 + 空值断口）全部跑通（画廊的 "Everything at once" 与 "ECG" 两页就是这条清单的现场）。

## 11. 性能验收

目标平台含工控机，指标保守：

| 场景 | 指标 |
|---|---|
| 10 万点折线首次出图（采样开） | < 250ms |
| 10 万点拖动 / 滚轮缩放 | 稳定 ≥ 30fps（单核可，不依赖 GPU） |
| 10 万点上 `FindDataPoints` | < 5ms |
| 实时 1 万点/秒合批追加 | UI 线程每帧 < 8ms，内存不随时间增长 |
| 100 万点常驻 | 常驻增量 < 50MB（依赖 §3 的 `ChartItem` 惰性物化） |
| light ↔ dark 切换 | < 100ms，且不重建 |

测量方式：数学层用 BenchmarkDotNet（`dotnet run -c Release --project src/AvCharts.Benchmarks`）；渲染帧率在目标机手测（CI 机器噪音大，**帧率不做 CI 门禁**）；CI 只跑粗粒度兜底预算（如"10 万点首次渲染 < 1s"）防灾难性回归。

**实测（10 万点、680×320，`DragBenchmarks`，同一次会话 `--job medium`，每格 16 帧一连取平均；分配量是确定值，时间是 15 次迭代的均值）**：

| 帧 | 时间/帧 | 分配/帧 |
|---|---|---|
| 窗口在动、抽稀可以留着用（`PanFrame`，基准） | **3.7 ms** | 651 KB |
| 窗口在缩放、每帧跨一次桶宽（`ZoomFrameAcrossABucketChange`） | **4.1 ms** | 434 KB |
| 窗口在动、feed 在流（`PanFrameWithTheFeedRunning`） | **4.4 ms** | 964 KB |
| 什么都没动、feed 在流（`FrameWithTheFeedRunningAndNothingMoving`） | **4.0 ms** | 701 KB |

要重算抽稀的那几帧比基准多 0.3–0.7 ms，那是**扫一遍读数**的价钱；分配上的差别来自 feed 自己（多一个读数进数组要翻倍增长），不是抽稀。`MinMax` 抽稀**每一帧都从同一份抽稀画**（平移与滚轮只改窗口、不改桶宽，所以重映射现成的那一份），于是"拖拽中的帧"与"静止的帧"是同一条路径 —— 画面**逐像素相同**（`DragThinningTests`，8 条）。

### 与 LiveCharts 的条件对齐（2026-10-14 / 15 实测）

一个对比只有在两边被要求做**同一件事**时才成立，而"同一件事"有两种，两种都量了（同一台机器、同一份 10 万点读数、同样 680×320、连续拖拽帧）：

| 条件 | LiveCharts 2.1.0-dev-798 | AvCharts | 倍数 |
|---|---|---|---|
| 两边都画全部 10 万点 | 262 ms/帧 | 30 ms/帧（`Sampling = None`） | ~8.8× |
| 两边都画约 1,300 点（数据本身只有这么多） | 9.1 ms/帧 | 2.0 ms/帧 | ~4.5× |
| 只有我们抽样（源仍是 10 万点，画约 1.3k） | —— 它没有这个功能 | 4.0 ms/帧 | |

读这张表的四条：它的数字来自它自己的**无头** `SKCartesianChart`、**不含控件层**（偏向它）；它用的是预发布构建；两边都按各自文档的平常写法驱动、都没调到极致；**BDN 会把机器切到高性能电源计划**，跨会话还有 ±30% 漂移 —— **只有同一会话内的倍数可读**。这张表里没有 SciChart：nuget.org 上没有 Avalonia 版，且它是商业库、运行时需要 licence key —— 用户拍板不测，所以性能一栏只有 LiveCharts 一行。另外，**LiveCharts（这个构建）没有降采样/抽稀**（`src/` 下 623 个 `.cs` 里没有实现，`Series` / `CoreLineSeries` / `Chart` 也没有相应 API），所以"两边都降采样"只能由使用者在**数据侧**先抽稀 —— 那就是上表第二行。

量法本身踩过的坑（单帧含进程预热的钱、两个电源计划不能混读）、抽稀那条路改形前后的数字与画面证据，都在 `docs/design-log.md`。

## 12. 工程化、测试、发布

- **继承房子模板**（`E:\cyberstone\slobber\iPaas.Controls` 已定型）：`Directory.Build.props`（`AvaloniaVersion` 单点引脚、`Nullable`、`ImplicitUsings`、`TreatWarningsAsErrors` 并放过 `NU1901-1904`、`EnforceCodeStyleInBuild`、`GenerateDocumentationFile`）、`Directory.Packages.props`（中央包管理 + `Microsoft.SourceLink.Gitea`）、`version.json` + **Nerdbank.GitVersioning**（版本由 git 历史算，只有 `v*` tag 是正式号，CI 不传 `-p:PackageVersion`）、`.github/workflows/`（**Gitea 读这个目录**；`ci.yml` / `release.yml` / `pages.yml`）、`nuget.config`（双源 + `packageSourceMapping` 避免 NU1507）、`slnx`、`build/*.ps1`。
- 额外加 **`Microsoft.CodeAnalysis.PublicApiAnalyzers`**（`PublicAPI.Shipped.txt` / `Unshipped.txt`）：任何公共 API 增删改都让构建失败。我们整套设计都在冻结契约，这个工具是契约的执行者。
- 目标框架 `net8.0;net10.0`（实测 Avalonia 12.1.0 只发这两个 TFM）。
- 测试三层：**纯单测**（变换 / 刻度梯级 / 堆叠 / 采样 / 值域 / 命中测试，不需要 UI 线程）→ **契约测试**（钩子顺序 + 参与清单 + 共享轴实例）→ **金图测试**（画廊**每一页**一张，加"每页的图不互相压"与"读数不画到绘图区外"两条不变式）。金图用 `Avalonia.Headless` + `Avalonia.Skia`，**字体钉死为 Inter**，**只在 Windows 上录与比**（别的平台自己跳过，由 `ci.yml` 的 `pictures` job 在 windows-latest 上守），容忍 ≤0.1% 像素差，基图只能在 `AVCHARTS_UPDATE_GOLDENS=1` 时写回，**录完必须逐张看过**。CI 需 `libfontconfig1` + 一套字体（房子模板已有此步）。
- 文档：英文 XML 注释**写全**（`GenerateDocumentationFile` 会让 CS1591 浮出来），**docfx** 生成 API 站（产物 `artifacts/docs/`）。
- 发布：**公开的 NuGet.org**，由 `.github/workflows/release.yml` 在 `v*` tag 上做：tag 触发 → `dotnet build` 整个解决方案 + `dotnet test` → `dotnet pack` 两个库 → 发布桌面画廊（win-x64 单文件）→ 用 **trusted publishing**（OIDC：`id-token: write` + `production` 环境 + nuget.org 上按仓库/工作流/环境命名的策略）换一把一小时有效的 key → `dotnet nuget push --source https://api.nuget.org/v3/index.json --skip-duplicate` → 写 GitHub Release（两个包 + snupkg + 画廊 exe）。**私有 BaGet 仍在 `nuget.config` 里**，是给组织内部的读取用的，不再是发布目的地。**0.1.1 已发布**（tag `v0.1.1`）。
- 站点：**GitHub Pages**，`pages.yml` 每次推 `master` 重建三块 —— docfx API 参考 `https://nandi.wang/av-charts/docs/`、浏览器版画廊 `https://nandi.wang/av-charts/gallery/`、手写落地页 `https://nandi.wang/av-charts/`。
- 仓库：`https://git.cyber-stone.com/ipaas/av-charts`（private 起步，Gitea 1.27.1）+ 镜像 `https://github.com/slobber/av-charts`（CI 与 Pages 跑在那边）。
- 许可：**MIT**（LICENSE 在仓库根、`PackageLicenseExpression=MIT`）。这是**独立实现**而不是 Flex 源码的移植（§1），Apache-2.0 的义务随**拷贝代码**而来、不随"照着接口重写"而来，所以 MIT 不与上游冲突；血统仍然**照实署名**（README 的 Lineage 一节），那是诚实不是义务。README 注明色板 "inspired by SciChart.js light theme, independently tuned" 不变。

## 13. 已完成的功能

**v1 的承诺全部兑现（0.1.1 已发布），v1.1 的清单也只剩 §14 里那几件。** 下表是"现在拿来就能用"的清单：每一项都能在画廊里看到活的例子（画廊的每一页同时是金图测试的输入，所以"文档说的"与"CI 画的"是同一份东西）。

| 功能 | 库侧入口 | 画廊里看得到 |
|---|---|---|
| **基座**：图层模型、主题装扮、调色板分配、命中测试、数据提示、十字线、选择与键盘语义、平移缩放、按尺寸/按更细栅格出图 | `ChartBase`（`SeriesLayer` / `AxisLayer` / `AnnotationLayer` / `CrosshairLayer` / `SelectionLayer`、`DataTip`）、`ChartBase.RenderImage` / `SaveImage` | 每一页（导出见 Export） |
| **笛卡尔图与五种尺度**：线性 / 分类 / 时刻 / 对数 / 时长，`Fit` 与 `Fixed` 两种尺度模式、`Interval` 自动吸附、`Separators`、主次网格间距 | `CartesianChart`、`LinearAxis`、`CategoryAxis`、`DateTimeAxis`、`LogAxis`、`TimeSpanAxis`、`AxisScaleMode` | Axes、ECG、Two scales |
| **轴的装饰**：刻度位置（外侧 / 里侧 / 交叉 / 无 **主次各一套**）、次刻度长度与自己的笔、标签旋转、标签错层、轴标题（方向与间隙）、轴上的分段（有名的一段接管它覆盖的标签）、标签避让 | `AxisRenderer.TickPlacement` / `MinorTickLength` / `MinorTickPlacement` / `LabelRotation` / `StaggerLabels` / `TitleGap` / `TitleDirection`、`AxisRenderer.Sections` + `AddSection` / `RemoveSection`、`IAxis.Title` | Axes、Two scales、Annotations |
| **线面族**：折线 / 面积 / 阶梯线 / 阶梯面积 / 散点（只有标记）/ 气泡（第三个读数定大小），曲线方式（直线与平滑）、五种标记形状与标记大小 | `LineSeries<T>`、`AreaSeries<T>`、`StepLineSeries<T>`、`StepAreaSeries<T>`、`PlotSeries<T>`、`BubbleSeries<T>`、`CurveMode`、`Markers`、`IMarkerRenderer` | Lines、Points |
| **带宽族**：柱 / 条，并排 / 堆叠 / 叠放，柱宽与最宽上限（`BandLayout`、`IColumn`） | `ColumnSeries<T>`、`BarSeries<T>`、`SeriesLayout`、`IStackable`、`StackTotals` | Columns、Bars |
| **金融**：蜡烛（实体 + 上下影线，涨跌两色来自主题）与开收刻线 | `CandlestickSeries<T>`、`HighLowCloseSeries<T>` | Candles |
| **统计**：区间、误差条、盒须（五数概括） | `RangeSeries<T>`、`ErrorSeries<T>`、`BoxSeries<T>` | Statistics |
| **饼与极坐标**：`PolarTransform`（角度 + 半径）；饼 / 圆环 / 玫瑰（半径即值）、炸开（整体与每块，可预留让位）、标签（块内 / 旁边带引出线 / 每块一张图）、嵌套多环、按块取色 | `PieChart`、`PieSeries<T>`、`PolarTransform`、`PieLabelPosition`、`ExplodeRadius` / `ExplodeSelector` / `ReserveExplodeRadius`、`RadiusByValue`、`InnerRadius` / `OuterRadius`、`LabelImageSelector` | Pie |
| **标注**：图上的一段（`Section`）、轴上的一段（`AxisSection`）、阈值线、事件标记、宿主自己的元素 | `Section`、`AxisSection`、`ChartElement`（含 `Visual` / `VisualAnchor`：元素可承载一件真控件）、`AnnotationLayer` | Annotations |
| **数据管道**：选择器（lambda 与 XAML 路径两条路共用一条内核）、`Filter`、空值三态（断口 / 连接 / 归零）、采样（`None` / `MinMax` / `Lttb` / `Average`，点数不超过像素列数时自动不生效）、有界窗口 `MaxReadings`、后台推送按帧合批、`ChartItem` 惰性视图、共享轴实例 | `Series<T>`、`ChartValue`、`NullValueMode`、`SamplingMode`、`Sampler`、`DataTransform`、`ChartItem` | Everything at once、ECG、Streaming、Oscilloscope |
| **主题**：两套 `ThemeDictionaries` + 全部令牌（§7）、按声明顺序取槽、宿主 `Resources` 局部覆盖、图例四个方位 | `ChartTheme`、`ChartTokens`、`Legend`、`LegendItem`、`LegendPlacement` | Theme、Palette、Two scales |
| **宿主默认**：一次配置、全库生效的主题与轴字面（ChartDefaults；简报里原名 `AvCharts.Configure(...)`，落地改名 —— 见 §14 与落地日志） | `ChartDefaults.Configure(...)`、`ChartDefaults.Current` | 不是画面（全局默认没有图可看，由测试守着） |
| **交互**：命中测试（读数 / 线段 / 台阶 / 面积 / 柱 / 条 / 饼）、每项事件与 `ICommand`、选择（None / Single / Multiple + 键盘语义 + **`Alt` + 拖拽的框选**）、数据提示（Item / Mouse / None + `TipStack`）、十字线、图例点击切可见、滚轮缩放与拖拽平移（Shift / Ctrl 单轴）、`VisibleRangeChanged` | `ChartBase.FindDataPoints` / `GetAllDataPoints` / `GetItemsInRegion`、`HitData`、`DataTipMode`、`ChartSelectionMode`、`PanBy` / `ZoomBy` | Interaction、Lines、Two scales |
| **滚动条**：横竖各一条，滑块的长度是**屏幕上占读数的份额**、位置是**窗口在读数里的位置**；拖滑块平移窗口、点轨道把窗口挪到指针处、最小滑块长度（`MinThumbLength`）、只读 `CanScroll`；轨道画在**绘图区自己那一段**上，默认细（8px）、**两端没有箭头按钮** | `ChartScrollBar`、`ChartTokens.ScrollbarTrack` / `ScrollbarThumb` / `ScrollbarThumbHover`、`DataTransform.DataRange` | Scrolling |
| **概览**：整段读数画成缩略形状（每条系列一条，空值断开）、窗口可拖中间平移、拖两端缩放（`ZoomBy`）、点别处跳过去；`CanScroll` 只读 | `ChartOverview`、`ChartTokens.OverviewBackground` / `OverviewWindowFill` / `OverviewWindowStroke`、`SeriesBase.Units` | Scrolling |
| **导出**：同一个图按指定尺寸**重新布局**出图、按更细的栅格出图（打印用）、PNG 写进任意 `Stream`（同步或**异步**——浏览器的 file handle 只认异步写）；画廊那一页还带一个**另存为**按钮（桌面弹保存框、浏览器走下载，都是 `IStorageProvider`）与一个自己缩放的预览 | `ChartBase.RenderImage`、`SaveImage`、`SaveImageAsync`（预览的缩放是宿主的事：见 `RenderImage` 的注释与落地日志） | Export |
| **多窗格**：两张图共用一根轴（价格 + 成交量那种），绘图区边缘对齐（`MinimumGutter` 把各自标签要的房间取齐），`Panes` 页用一个旋钮把"对齐 / 各留各的"两种状态摆出来 | `AxisRenderer.MinimumGutter`（库侧唯一新增） | Panes |
| **XAML**：`avc:` 前缀（`https://github.com/slobber/av-charts`）、十四个非泛型系列（每个系列一份）、路径选择器（`XPath` / `YPath` / `OpenPath` / `ValuePath` / …）、示例按"结果 / 代码 / axaml"三个 tab 显示（axaml 就是跑起来的那个文件；**十一个例子**，每个图表页 1–3 个，两半由测试逐像素对着 ✓） | `XmlnsDefinition`、`LineSeries : LineSeries<object>` 一族、`Examples/` 下的 11 对文件 | Lines、Columns、Bars、Points、Candles、Statistics、Pie、Theme |
| **工程**：公共 API 门禁（PublicAPI 分析器）、三层测试 + 画廊逐页金图、基准、CI（build / test / 图片 / 站点）、NuGet 发布（trusted publishing）、MIT | — | — |

## 14. 未决项与待办

**未决（需要决定）**

- **动画骨架的具体形态**：v1 只承诺"每帧只重录受影响 layer"，动画本身不做；分档 zoom（吸附到预设档位）也还没做。

**待办（已经定了要做、还没做）**

- **多图共享轴 UI、刻度对齐**（§10）：一个轴实例服务多个图表已经成立（有测试），"多张图对齐刻度"的 UI 没有。


**挂账（已知的技术债 / 待查）**

- **几何按参考映射建一次 + 绘制矩阵改写**：现在一次重排 = 一次几何重建（十万点约 600 KB/帧），平移只改绘制时的变换矩阵就能省掉它。
- **浏览器里量布局次数**：推送路径改成增量之后（滑窗页每帧 1 次布局），在浏览器里装回探针、发一份 AOT 产物量一次，看这笔值多少。
