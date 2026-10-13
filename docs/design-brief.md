# AvCharts 设计共识（决策记录）

> 状态：**待"可以动工"确认**。本文是 `/grill-me` 拷问过程的产物，记录已达成的决定、理由与出处，以及明确的未决项。
> 语言约定：本文件用中文（内部决策记录）；**代码注释、XML 文档、README、NuGet 描述、样例说明一律英文**。
> 事实纪律：文中标注"实测"的结论都来自本机可查的证据（Apache Flex 源码、Avalonia 12.1 包、Gitea API、LiveCharts 文档站），不凭印象。

## 0. 一句话定位

C#/Avalonia 12 版的 `mx.charts`：继承 Flex 图表的**架构与类型名**，成员命名与工程实践 C#/Avalonia 化；默认配色**不复用 Halo**，以 SciChart.js light 主题为灵感重新调色，并**分别**为 light/dark 调色。

## 1. 项目形态与命名

- 目标是**公开库**（发 NuGet、要 API 稳定性承诺），但按个人库的节奏交付：v1 之前不投入发布工程。
- 命名策略 **B 档**：类型名逐字继承（`ChartBase` / `CartesianChart` / `LineSeries` / `LinearAxis` / `AxisRenderer` / `ChartItem` / `DataTransform` / `ChartState` …），成员名 C# 化（PascalCase、枚举代替字符串常量、`[Flags]` 代替 uint 位掩码、`double?` 代替 `NaN` 哨兵）。
- 命名空间 `AvCharts`；领域类型 `AvCharts.Charts`，主题 `AvCharts.Themes`。
- 程序集边界（**已冻结**：XAML 的 `clr-namespace:…;assembly=…` 映射使搬类型成为破坏性变更）：

```
src/AvCharts/          命名空间 AvCharts.Charts   模型、DataTransform、轴、series、ChartItem、绘制内核
src/AvCharts.Controls/ 命名空间 AvCharts.Controls / AvCharts.Themes   ChartBase:Control、DataTip、Legend、ControlTheme、主题资源
src/AvCharts.Demo/     画廊（门面 + 金图视觉资产来源），net10.0
src/AvCharts.Tests/    单测 + 契约测试 + 金图测试（跟房子模板一样放在 src/ 下）
```

- 硬约束：`AvCharts` **绝不依赖 `Avalonia.Controls`** —— 这层拆分的全部意义。
- **落地修正**：Avalonia 12 **没有** 独立的 `Avalonia.Base` / `Avalonia.Controls` / `Avalonia.Markup.Xaml` 包（前两个在 nuget.org 上都是 404），三者在同一个 `Avalonia` 元包里。所以这条约束无法靠包引用表达，改由**测试**守住：`AssemblyBoundaryTests` 断言 `AvCharts.dll` 的引用程序集里不含 `Avalonia.Controls` / `Avalonia.Markup.Xaml`。约束不变，执行方式变了。
- 类型命名空间即层：`AvCharts.Charts`（模型）/ `AvCharts.Controls`（控件）/ `AvCharts.Themes`（主题）。
- 将来若要做"真中立核心"（OxyPlot / LiveCharts 式），入口设在 `AvCharts` 内部已有的绘制后端 seam 上，**不新增公共抽象**。

## 2. 渲染架构（决策 C）

- 图表是自定义 `Control`，内部固定若干 layer 视觉：背景元素 / 网格线 / series / 标注 / 轴 / DataTip / Legend。
- `Series`、`ChartItem`、`Axis`、`AxisRenderer` 都是**模型对象，不是 Visual**；命中测试由我们自算（Flex 本来也是自算的：`DataTransform.invertTransform` + `findDataPoints`）。
- 每个 layer 只做批量绘制（同层几何合并），悬停/选中只在受影响的 layer 上 `InvalidateVisual`。
- series 层内部再拆**基础 bucket**（批量几何，数据/轴/主题变化才重录）与**交互 overlay**（只画高亮项），使悬停成本是 O(高亮点数) 而非 O(N)。
- 例外：**DataTip 与 Legend 是真控件**（吃到 ControlTheme、可换皮、可用模板）。
- 空心/半透明标记的悬停覆盖会有重影：悬停项变化时把该点从基础 bucket 排除并重录一次基础层（人类手速下 10k 点约 1ms），动画帧只重录 overlay。
- 补丁（原 Q3 的"不支持任意 XAML 内容"）：**S4 标注元素可以声明为"承载 Visual"**，由 chart 在 overlay 层挂少量真控件（LiveCharts 的 VisualElements 模式）。不是"每个数据点一个控件"。

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
- `AxisBase.PixelsPerUnit`（unit 按轴类型定义：Linear = 1 数据值、DateTime = 1ms、Log = 1 数量级、Category = 1 槽位 —— 与 `unitSize` 既有语义对齐）。`AxisBase.UnitSize` 保持 Flex 的**只读派生**语义不动（原文："The size of one unit of data as represented by this axis"，`DateTimeAxis` 实现为"一天多少毫秒"）。
- Fixed 模式三条语义：**`Minimum` 是视窗锚点，`Maximum` 是派生量**（设置被忽略 + Debug 警告）；**锚点对齐到 `Interval` 整数倍、网格铺满绘图区、边缘允许残缺末格**（不量化绘图区，否则图表不填满容器）；分类轴多出的空槽位只画网格不画标签。
- 刻度：`Interval` 为 `double?`，**null = 自动吸附**（等格默认行为：由 `MajorGridPitch` 与 `PixelsPerUnit` 反推并吸附到漂亮档位），显式给值则固定刻度。`MajorGridPitch` 默认 60px、`MinorGridPitch` 默认 12px（5:1，沿用图纸习惯）。
- 诚实说明：**"格子像素严格恒定"数学上做不到**（除非放弃漂亮刻度值）—— 实际会在目标值附近按档摆动（1/2/5 梯级约 ±60%，细化到 1/2/2.5/5 约 ±33%）。**分档 zoom**（吸附到预设档位）推 v1.1。
- 梯级按轴类型：数值轴 1/2/2.5/5 十进制；时间轴必须日历感知（ms/s/min/h/day/month…）；对数轴按数量级。梯级策略可被自定义轴覆盖，但**不作为 v1 公开配置**。
- `Axis.Separators`（显式刻度值列表，LiveCharts 有、Flex 无）纳入 v1。
- 网格线**吸附设备像素**（只取整位置、不取整间距）—— 波形比例优先，网格锐利靠吸附。
- 自定义轴契约（S2）：值↔单位变换、刻度生成、标签格式化、`DescribeData` 声明的字段需求 —— 心电那种 0.04s / 0.2s 非十进制梯级靠它。

## 7. 主题

- 机制：`ControlTheme` + `ThemeDictionaries`（Light/Dark 两套），资源键是**公共 API** 并冻结；图表订阅 `ActualThemeVariantChanged` 做重绘（不重建）。宿主用 `Resources` 覆盖任意令牌即可（一张图单独换皮不需要新 API）。
- 令牌全集（**类型已定**：填充类 `IBrush`，描边类 `Pen` —— 渐变与虚线因此免费，对应 LiveCharts 的 "Paints"）：

| 组 | 令牌 |
|---|---|
| 表面 | `Surface.Background`、`Surface.Border`、`Surface.FocusRing` |
| 轴带 | `Axis.Band.Fill`、`Axis.Band.FillAlt` |
| 轴 | `Axis.Line`、`Axis.Tick`、`Axis.Label.Foreground`、`Axis.Title.Foreground` |
| 网格 | `Axis.GridLine.Major`、`Axis.GridLine.Minor`、`Axis.GridLine.Origin` |
| 数据 | `Series.Stroke1..N`（`Pen`）、`Series.Fill1..N`（`IBrush`） |
| 标记 | `Marker.Fill`、`Marker.Stroke`、`Marker.Hover.*`、`Marker.Selected.*` |
| 十字线 | `Crosshair.Line`、`Crosshair.Label.Background`、`Crosshair.Label.Foreground` |
| 图例 | `Legend.Background`、`Legend.Border`、`Legend.Foreground`、`Legend.Item.Disabled` |
| 提示 | `DataTip.Background`、`DataTip.Border`、`DataTip.Foreground`、`DataTip.Shadow` |
| 金融 | `Financial.Up`、`Financial.Down` |
| 选择区 | `Selection.RubberBand.Fill`、`Selection.RubberBand.Stroke` |

`Axis.GridLine.Origin` 单列的理由：Flex 的 `GridLines` 专门有 `horizontalShowOrigin` / `verticalShowOrigin`（轴原点线，通常更粗更深，即工程图里的零线），SciChart 只是拿 `gridBorderBrush` 糊过去 —— 它必须能单独控制。

- 分配语义：chart 持有有序调色板，按 series **声明顺序**取槽；legend 与 data tip 同源；`PieSeries` 按数据点索引取色；**显式设了 Fill/Stroke 的 series 不占槽位**（修掉 Flex 里"给首个系列设色导致后续全体错位"的坏味道）。
- 色板：**前 5 个沿用 SciChart.js light 主题**（实测 `SciChartJSLightTheme.js`：`#F48420` `#AE408E` `#30BC9A` `#209FD9` `#264B93`，fill = 同色 @47%），按同风格扩到 **10 个**（5 色对饼图/多系列不够用）；**dark 单独调一组**（提亮、降饱和）。依据：SciChart 自己在 dark 主题里也改了色板（去掉青绿、背景 `#1C1C1E`、刻度字 `#A6A7AC`、主网格 `#323539`、次网格 `#232426`、轴带 `#202123`），说明同一组颜色跨明暗不成立。
- 由对照图得到的结论：**折线默认不填充** —— 47% 的 fill 只服务面积图与柱/条（预览图里 S1 的面积色块很直观）。
- 与房子设计系统的关系：`iPaas.Controls` 已有 `Chart1..5`（**同一色相的 5 个明度档** `#8EC5FF`→`#193CB8`，light/dark 相同），适合分档、不适合区分系列。av-charts 自带多色相默认板，接入宿主时用资源键覆盖即可，**不内建单色 ramp 机制**。
- 候选色板（见 `tools/palette-preview.py` 与其产出的对照图）：
  - light 描边：`#F48420` `#AE408E` `#30BC9A` `#209FD9` `#264B93` `#7A9A2E` `#C25E5E` `#7C6BB8` `#B08A3E` `#5E7387`
  - dark 描边：`#FF9E45` `#D06BAE` `#45D6B2` `#4CB6E8` `#6C8FD8` `#A8C05C` `#E08080` `#9E90D8` `#D8B45E` `#8C9CAC`
  - light 中性色沿用 SciChart：surface `#F9F9F9`、刻度字 `#333333`、主/次网格 `#CFCFCF`(+47%)、轴带 `#AAAAAA`@3.5%、轴标题 `#777777`
  - 定稿前建议做色觉障碍（deuteranopia）可分辨性检查。注意 `#264B93` 深蓝在深底上几乎不可见，dark 的第 5 色因此提亮成 `#6C8FD8`。

## 8. 交互（v1 范围）

- 命中测试：`FindDataPoints(Point)` / `GetAllDataPoints()` / `GetItemsInRegion(Rect)` / `MouseSensitivity`（默认 5px，Flex 同名同默认），`HitData` 带 `Distance` / `Item` / `Element` / `ChartItem` / `X` / `Y` / `SeriesColor` / `DisplayText`。带空间索引。
- 事件：`ItemClick`、`ItemDoubleClick`、`ItemPointerPressed/Moved/Released/Entered/Exited`、`SelectionChanged`（`ChartItemEventArgs`）；另提供 **`ICommand` 属性**（MVVM，Avalonia 用户主力）。文档要写明：数据点不是 Visual，**这些"每项事件"是 chart 在命中测试结果变化时合成的**。
- 选择：`SelectionMode { None, Single, Multiple }` + 完整键盘语义（方向键移动、Space 选择、**Ctrl 只移动 caret 不改选择、Shift 从锚点区间选择**；实测 Flex `handleNavigation`）。需要 `Focusable` 与焦点视觉令牌。
- DataTip：`DataTipMode { Item, Mouse, None }` + `ShowAllDataTips` + 模板/内容定制 + **保留 Flex 的 `TipStack` 防重叠堆叠**（LiveCharts 没有，我们要比它好）。
- 十字线：**v1 纳入**（专业场景标配，成本低，正好用 overlay 层）。默认 **crosshair 关、data tip 开**。
- 图例点击切换系列可见性：**v1 纳入**（Flex 没有：`Series` 连 `visible` 属性都没有，隐藏系列要靠应用层自己写）。需要 `Series.Visible` + `Legend.Item.Disabled`，且隐藏系列不参与轴值域与布局。
- pan / zoom：滚轮 = 双轴同步缩放（锚在指针）；拖拽 = 平移；`Shift` = 仅横轴、`Ctrl` = 仅纵轴。**Fixed 模式 zoom 写 `PixelsPerUnit`、pan 写 `Minimum`；Fit 模式两者都写进 `Minimum`/`Maximum`** —— 两种模式都退化成单纯的属性写入。
- **为 v1.1 滚动条/概览条/联动做三件底层准备（v1 就做）**：
  1. 公开的视窗操作 API（`PanBy` / `ZoomBy(factor, anchor)`），鼠标、滚轮、键盘、滚动条、外部代码共用一条路径；
  2. 图表级坐标换算 `DataToPoint(...)` / `PointToData(Point)`（LiveCharts 的 `ScalePixelsToData` 就是这个作用：其 scrollable 示例通篇是应用层代码，库本身并没有滚动条控件）；
  3. `VisibleRangeChanged` 事件。
- 多图联动**不需要新机制**：实测 Flex `AxisBase.registerDataTransform(transform, dimension)` 把 transform 推进数组、`describeData` 对全部注册者聚合 → **一个轴实例可服务多个图表**。v1 写测试锁住这个性质。
- 框选区间（Flex `RangeSelector`）留 v1.1；`Selection.RubberBand.*` 令牌先留着不实现。拖拽改数据**不做**。

## 9. 扩展契约（v1 冻结形状）

- **S1 自定义系列**：继承 `Series<T>`，重写 `UpdateData → DescribeData → BuildGeometry → Draw`；提供 `SeriesRenderContext`（`PlotArea` / `ValueToPoint` / `Clip`）；必须能重写 `HitTestItem(Point) → HitData?`（默认实现 = 值位置 + `MouseSensitivity` 半径）。
- **S2 自定义轴**：继承 `AxisBase`（见 §6）。
- **S3 自定义标记**：`IMarkerRenderer`，**接口组合**不走继承；内置覆盖 Flex `renderers/` 那 11 个。
- **S4 自定义图层元素**：非 series 的元素基类 + `CartesianDataCanvas` 的现代对应物（阈值线、参考区、事件标记）；v1 文档标 **experimental**；可承载 Visual（见 §2 补丁）。
- **钩子顺序是公开契约**（Flex 把三阶段当契约文档化），配契约测试断言顺序。
- 主干用继承 + `protected virtual`；**能力用接口**（堆叠走 `IStackable`，与 Flex 一致）。
- `DataTransform` 公开可注入（标 advanced）—— 自定义坐标系的唯一入口，后续饼图 / 能力六边图（雷达）靠它。

## 10. v1 覆盖矩阵

| 族 | v1 | v1.1 | v2 / 不做 |
|---|---|---|---|
| 点线面 | Line / Area / Plot / Bubble + StepLine / StepArea | | |
| 带宽 | Column / Bar（cluster / overlay / stacked） | | |
| 轴 | Linear / Category / DateTime / Log / TimeSpan | | |
| 饼图 | | PieChart（PolarTransform） | |
| 金融 | | Candlestick / HLOC | |
| 统计 | | Box / Error / Range（置信区间） | |
| 标注 | | Sections / Axis Sections | 自由绘制层（CartesianDataCanvas） |
| 热力 | | | Heat series |
| 极坐标 | | | Gauge / 南丁格尔玫瑰 / 嵌套饼 / 太阳图 |
| 共享轴 | 共享轴实例（配测试） | 多图共享轴 UI、刻度对齐 | |
| 导出 | | 图表导出图片 | |
| 动画 | 不做（只留"每帧只重录受影响 layer"的骨架） | SeriesInterpolate / Slide / Zoom、分档 zoom | |
| 其他 | 十字线、图例切换可见性、`ICommand` | `AvCharts.Configure(...)` 全局默认、滚动条/概览条 | 拖拽改数据、地图、轴分页 |

保留 Flex 的图表类型类（`LineChart` / `ColumnChart` / …）作为迁移锚点与文档载体，但**系列类型约束降级为"默认预设"而非硬限制**（Flex 的约束本只是文档级，柱+线组合图能用）；`ChartBase.type`（`cluster` / `overlay` / `stacked`）按 B 档改名 `SeriesLayout` 枚举。

**v1 功能验收**：Column + Line 组合图、双 Y 轴（Flex 原生支持：`Series.horizontalAxis` / `verticalAxis` + `getAxis` / `setAxis`）、时间轴、堆叠柱、十万点散点、light/dark 切换、data tip、十字线、图例切换、ECG（Fixed 尺度 + 等格 + 采样 + 空值断口）全部跑通。

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

测量方式：数学层用 BenchmarkDotNet；渲染帧率在目标机手测（CI 机器噪音大，**帧率不做 CI 门禁**）；CI 只跑粗粒度兜底预算（如"10 万点首次渲染 < 1s"）防灾难性回归。

## 12. 工程化、测试、发布

- **继承房子模板**（`E:\cyberstone\slobber\iPaas.Controls` 已定型）：`Directory.Build.props`（`AvaloniaVersion` 单点引脚、`Nullable`、`ImplicitUsings`、`TreatWarningsAsErrors` 并放过 `NU1901-1904`、`EnforceCodeStyleInBuild`、`GenerateDocumentationFile`）、`Directory.Packages.props`（中央包管理 + `Microsoft.SourceLink.Gitea`）、`version.json` + **Nerdbank.GitVersioning**（版本由 git 历史算，只有 `v*` tag 是正式号，CI 不传 `-p:PackageVersion`）、`.github/workflows/{ci,nuget-publish}.yml`（**Gitea 读 `.github/workflows/`**）、`nuget.config`（双源 + `packageSourceMapping` 避免 NU1507）、`slnx`、`build/*.ps1`。
- 额外加 **`Microsoft.CodeAnalysis.PublicApiAnalyzers`**（`PublicAPI.Shipped.txt` / `Unshipped.txt`）：任何公共 API 增删改都让构建失败。我们整套设计都在冻结契约，这个工具是契约的执行者。
- 目标框架 `net8.0;net10.0`（实测 Avalonia 12.1.0 只发这两个 TFM）。
- 测试三层：**纯单测**（变换/刻度梯级/堆叠/采样/值域/命中测试，不需要 UI 线程）→ **契约测试**（钩子顺序 + 参与清单 + 共享轴实例）→ **金图测试**。金图用 `Avalonia.Headless` 的 `HeadlessWindowExtensions.CaptureRenderedFrame` + `AvaloniaHeadlessPlatformOptions.UseHeadlessDrawing=false` + `Avalonia.Skia`（API 与文档原文已实测确认），**字体钉死为 Inter** 保证确定性，**只在一个平台出基准**，容忍阈值 ≤0.1% 像素差，基图只能在 `AVCHARTS_UPDATE_GOLDENS=1` 时写回。CI 需 `libfontconfig1` + 一套字体（房子模板已有此步）。
- 文档：英文 XML 注释**写全**（`GenerateDocumentationFile` 会让 CS1591 浮出来），最后用 **docfx** 生成 API 站（产物 `artifacts/docs/`，托管方式后置）。
- 发布：私有 BaGet `https://baget.cyber-stone.com`（实测是活的 BaGet，`PackagePublish/2.0.0` 在 `/api/v2/package`）：`dotnet nuget push --source https://baget.cyber-stone.com/api/v2/package --api-key $NUGET_KEY --skip-duplicate`，密钥是仓库 secret `NUGET_KEY`（可选 `BAGET_USERNAME` / `BAGET_TOKEN` 供读取认证）。
- 仓库：`https://git.cyber-stone.com/ipaas/av-charts`（**已定 private 起步**：房子默认形状是 public + master + Actions 开，但"public + 不加许可 = 保留所有权利"与"将来公开的库"矛盾，故先 private，定许可时再转 public）。Gitea 1.27.1；`ipaas` 组织 25 个仓库中 10 个启用了 Actions，所以 CI 可用。本地目录已改名 `E:\GitHub\slobber\av-charts`。
- 许可：**暂不加**。将来公开时建议 Apache-2.0 + `NOTICE`（架构与 API 设计源自 Apache Flex 的 `mx.charts`，按其 §4(d) 带上 Flex 的 NOTICE）；README 注明色板 "inspired by SciChart.js light theme, independently tuned"。

## 13. 明确的未决项

1. 深色主题 surface 基色取值（预览暂用 `#1C1C1E`）。
2. 动画骨架的具体形态（v1 只承诺"每帧只重录受影响 layer"）。
3. 分类轴在 Fixed 模式下"格子像素恒定不成立"（离散不可发明中间刻度）需在文档中明示。
4. docfx 站点托管位置。

已关闭：**light/dark 色板已定稿**（就是 §7 那 10 + 10 个值，对照图见 `tools/palette-preview.py`）；色觉可分辨性检查挪到画廊里做同屏对比时一并看。

## 14. 落地进度

- 2026-10-13：仓库 `git.cyber-stone.com/ipaas/av-charts` 建好（**private**，Actions 已启用），本地目录 `E:\GitHub\slobber\av-charts`，默认分支 `master`。
- 首个提交 `7dec4c8`：房子模板落地（两个库 + 画廊 + 测试、NBGV、SourceLink、PublicApiAnalyzers、两个 workflow、release 脚本、nuget.config、README、本文件）+ 第一个模型类型 `AvCharts.Charts.ChartValue` 及其测试。
- 版本号：`version.json` 起于 `0.1.0`，无 tag 时 NBGV 产出形如 `0.1.0-g7dec4c8e32` 的预发布号（排序低于 `0.1.0`，内部消费安全）。
- 环境注意：本线程的应用工作目录仍指向改名前的 `av-chart`，因此编辑文件要经 `../av-charts/...` 相对路径落到真实仓库。
- 工作流规格：`workflows/avcharts-v1-step-loop.md`（心跳 `avcharts-v1-step-loop`，30 分钟）定义"逐步推进 v1"这个 loop：条目=检查点、步骤=提交单位，四种叫醒条件，以及无人值守时的自主边界。
- 2026-10-13 条目 1 · 步骤 1.1 完成：`ChartDimension`（开放维度类型，`X`/`Y`/`Radius`/`Angle`）、`BoundedValue` + `DataDescription`（元素向轴声明值域，空值必须不写进去）、`IAxis` + `AxisUnitRange`（单位空间、`Normalize`/`Denormalize`、退化区间落到中点）、`IChartElement`、`DataTransform`（轴注册、`PlotBounds`、值↔屏幕双向映射、`DescribeData` 聚合后交给轴、惰性幂等的 `Update`、快照通知）。Y 轴按屏幕坐标翻转。测试 25 个全绿（含"降采样时元素自注册不破坏通知遍历"这条边界）。
- 2026-10-13 条目 1 · 步骤 1.2 完成：`AxisBase` + `LinearAxis`。**过程中改了一处契约**：轴从"存结果"改成"纯计算"—— `IAxis.SetDataBounds` 与 `IAxis.UnitRange` 换成 `IAxis.Resolve(bounds, lengthInPixels) → AxisLayout`，算出来的值域与刻度按维度存在 `DataTransform` 上（`GetLayout(dimension)`）。理由是 §8 承诺过"一个轴实例驱动多张图"：若结果存在轴上，最宽的那张图会覆盖其余图的可见值域。另外：`AxisScaleMode.Fixed` 下 `Maximum` 被忽略（`IsMaximumIgnored` 可查）、锚点按 `Interval` 取整、`PixelsPerUnit ≤ 0` 或无长度时退化为 `Fit`；自动间隔取"不小于目标值的漂亮档"（1/2/2.5/5，因此格距最多在目标值附近 2 倍内摆动）；无数据/单值时值域为空、不发明窗口、不画网格；`Separators` 显式刻度替代生成值并按值域过滤；轴设置变化通过 `Changed` 事件让 transform 重新解析。测试 41 个全绿。
- 2026-10-13 条目 2 · 步骤 2.1 完成：绘制内核。`ChartLayer : Visual`（图层基类：`Configure(plotBounds, renderScaling)` 只在有变化时请求重绘，`Render` 封口到 `DrawLayer`，默认裁剪到自己边界）、`SeriesRenderContext`（`Transform`/`PlotBounds`/`RenderScaling` 加设备像素吸附：`Snap` 取整、`SnapStroke` 让奇数宽描边落在半像素上、偶数宽落在边界上）、`GeometryBatch`（把成千上万个形状合成一个 `StreamGeometry`：折线/多边形/矩形/圆/菱形，一次构建、重复构建抛异常）。**测试基础设施**：`Avalonia.Headless` + `Avalonia.Skia`（`UseHeadlessDrawing = false`）按需起平台并作为 xunit collection fixture —— 几何与像素断言没有真实绘制后端就没有意义。踩到并照房子解决的坑：`Avalonia.Headless.XUnit` 在 12.x 已依赖 xunit v3，与房子的 xunit 2.9.3 冲突（`FactAttribute` 双来源），所以用纯 `Avalonia.Headless` 加手工 `SetupWithoutStarting()`。测试 53 个全绿。
- 2026-10-13 条目 2 · 步骤 2.2 完成：`Series`（四阶段钩子 `UpdateData → DescribeData → BuildGeometry → Draw` 写进文档；`Paint` 先把 transform 的 describe 跑完再建几何，保证顺序；`Invalidated` 事件把重绘交给图层）、`CartesianSeries<T>`（`ItemsSource`/`XSelector`/`YSelector`/`Filter`，X 缺省用序号；值一次读入成 `ChartValue` 对，`Values` 读取时自动补读；`describeData` 只声明轴能理解的值）、`LineSeries<T>`（线与标记是两个几何：标记一个填充、线一个描边；标记默认关闭）、`IMarkerRenderer` + `Markers.Circle/Square/Diamond`、`SeriesLayer : ChartLayer`（把重绘收敛到一层）。空值/不可定位值**断开成多段 run** 而不是落到原点。测试 63 个全绿，其中一条用 `RenderTargetBitmap` 断言真的出了墨。
- 2026-10-13 条目 2 · 步骤 2.3a 完成：`AxisRenderer`（模型侧，不是 Visual）：`GetTicks(layout, plotBounds)` 把轴算出来的单位刻度换成像素位置与标签文本，`MeasureGutter(layout)` 用 `FormattedText` 量文字算出这一维需要多少像素（gutter 从**全部**标签取最宽者，避免"抽稀改变 gutter → 绘图区移动 → 又抽稀"的循环）。标签抽稀按**轴向**判断相邻区间而不是按屏幕方向（竖直轴的值往上走、位置却往下走 —— 第一版就栽在这里）；近端必留、远端在还放得下时补留。`IAxis` 增加 `FormatValue(double)`（§9 的 S2 契约本来就含"标签格式化"）。72 个测试绿。**记账**：原步骤 2.3 拆成 2.3a（本次）与 2.3b（图层与控件接线），因为一个提交塞不下。
- 2026-10-13 条目 2 · 步骤 2.3b 完成：图层与控件接线。`ChartLayer` 改成 `Layoutable`（实测 `Avalonia.Layout.Layoutable` 就在 **Avalonia.Base**，所以在守住"核心不碰 Avalonia.Controls"的同时拿到真正的边界与裁剪）；`AxisLayer` 画网格（网格按**全部**间隔、标记与标签只画抽稀后留下的那些 —— 密网格配疏标签，正是固定格距的用途）、轴线、刻度线与标签；`ChartBase : Control` 持有 transform 与固定几个图层、`Series` 用 `AvaloniaList<Series>` 并在增删时接上/摘下 transform 与 series 图层；布局用**迭代**求绘图区（gutter 依赖标签、标签数量依赖绘图区长度，最多 4 轮收敛）。`CartesianChart` 提供 `HorizontalAxis`/`VerticalAxis` 与两个 renderer。79 个测试绿（含"加系列会接上 transform、移除会摘掉"和"两条图层各自真的出墨"）。**遗留**：Avalonia 12 里 `VisualChildrenCount`/`GetVisualChild` 已不可重写，改用 `VisualChildren.Add`；"视觉树真的会渲染这些图层"这件事由 2.4 的金图负责证明。
- 2026-10-13 条目 2 · 步骤 2.4 完成（**条目 2 收口**）：画廊画出第一条真实折线，第一张金图 `src/AvCharts.Tests/Golden/Approved/first-line-chart.png` 入库。**架构风险解除**：`RenderTargetBitmap.Render(control)` 确实遍历视觉树，所以用 `VisualChildren.Add` 挂上去的图层会被渲染 —— 这一点由金图测试里的 `Ink.Count > 0` 断言证明，也是 2.3b 留下的那个悬念的答案。**金图基础设施**（§12 的机制落地）：`AVCHARTS_UPDATE_GOLDENS=1` 才写基准图（平时只读，构建不会偷偷改自己的期望）、失败时把实际渲染留在 `Golden/Received/`（git 忽略）、0.1% 像素容差 + 每通道 8 的漂移容忍、字体钉死 `WithInterFont()`（标签的测量与绘制用同一字体，图才不会因机器而异）、渲染尺寸 400×200。金图暴露的第一个问题：最宽的标签正好贴到控件左边缘（gutter = 4+4+12 = 20，标签起点 x=0），看着像被切 —— 于是 `AxisRenderer` 增加 `LabelMargin`（默认 4px 的余量），并补两条不变式测试（"测量的宽度不窄于画出来的宽度"、"绘图区让出的 gutter 不小于标签要的 gutter"）。82 个测试绿。新增 `Ink.Bounds` 用于按墨迹范围断言。
- 2026-10-13 条目 3 · 步骤 3.1 完成：`DateTimeAxis`，并按用户意见把时刻类型定为 `DateTimeOffset`。要点：(1) **单位空间起点钉在 Unix 纪元**——按公元 1 年计时的话，2026 的量级下一个 double ULP 就是 ~15µs，正好把 10µs 步长量化掉；钉到 1970 后 ULP ≈ 240ns，实测 10µs 步长误差 **10 纳秒**、1µs 步长仍可分辨（测试里把这两个数都钉住了）；差值在 long 里先算再除，不做大数相减。(2) **`ChartValue` 承载 `DateTimeOffset`** 而不是 `DateTime`：前者自带偏移，按 `UtcTicks` 排序不必猜；`FromDateTime` 对 Local 保留本机偏移、对 Unspecified 按原样（offset 0）——避免"同一份数据在不同机器上画成不同位置"。(3) 标签钟面由 `DisplayOffset` 决定：不设时按各值自带偏移（墙壁时钟读数就还是那个读数），设了就统一到某个偏移。(4) 梯级：10/20/50µs、100/200/500µs、1..500ms、1/2/5/10/15/30s、分、时、1/2/7 天；超过一周回落到整日（自然月/年登记为 3.1b）。(5) 标签精度随步长自适应：µs 级 `HH:mm:ss.ffffff`、秒级 `HH:mm:ss`、日级 `yyyy-MM-dd`。93 个测试绿。**遗留待办**：`CartesianSeries<T>` 目前把值存成 `List<(ChartValue, ChartValue)>`（每点 ~64-80 字节），100 万点会顶破 §11 的 50MB 预算；归一化缓存应改为单位空间的 `double[]`（每点 16 字节），`ChartValue` 只是入口形态——登记到条目 5 一起做。
- 2026-10-13 条目 3 · 步骤 3.2a 完成：`TimeSpanAxis` + 抽出 `ClockLadder`（钟表梯级与小数位规则由 `DateTimeAxis` 与 `TimeSpanAxis` 共用，避免两份会走样的副本）。时长以毫秒数入轴：`ChartValue` 没有时长种类，加一个会牵动结构体大小与入口管线，而选择器写 `item.Elapsed.TotalMilliseconds` 本来就很自然——这个取舍写进了类型的文档注释。标签是秒表的读法：`01:02:03.456`，跨天写 `3.01:02:03`（时长没有日期，绝不写成星期几）；未设 `Interval` 时按数值自身的最小有效位决定小数位（整秒给 3 位以外的 0 位、半秒给 3 位、半毫秒给 6 位）。负时长与普通数值一样照常放置。99 个测试绿。
- 2026-10-13 条目 3 · 步骤 3.2b 完成：`LogAxis` —— 单位空间是十进制指数（`TryToUnit` 取 `log10`、`FromUnit` 取 `10^unit`），于是屏幕上等距即数据中同比例。**0 与负值没有位置**：`TryToUnit` 返回 false，点按缺失处理（与 §4 的空值政策一致），而不是被挪到某个方便的地方。刻度落在 10 的幂上，间隔从数量级梯级 1/2/5 里取且**不细于一个数量级**；比一个数量级更窄的跨度因此只有很少几条线 —— 印在纸上的对数图那种"每个数量级内画 1-2-5"需要非均匀刻度生成，和自然月/年（3.1b）是同一个限制，一并记档而非假装做到。105 个测试绿。
- 2026-10-13 条目 3 · 步骤 3.3 完成：`CategoryAxis` —— 单位空间是槽位号，槽位按类别**首次出现的顺序**发放（数据到达的顺序；`ClearCategories()` 用于整体换数据）。这是"轴只存设置、不存结果"原则的**唯一例外**：类别表只能从数据里学，没有别的地方可来，这一点写进了类型的文档注释而不是藏起来。标签是类别名；`FormatValue` 只在标签正好站在槽位上时给名字，否则给数字（错的类别名比数字更糟）。刻度落在整槽（`NiceInterval` 取整，分数槽位没有意义）。**半个槽位的端部余量**（让首尾柱不被切一半）留给带宽布局那一步（6.1），与 §6 里"分类轴 Fixed 下空槽只画网格"的约定一起做。注册类别时会触发 `Invalidate()` → 描述遍历会多跑一轮，第二轮回落到稳定状态（已注释说明）。112 个测试绿。
- 2026-10-13 条目 3 · 步骤 3.4a 完成：`Sampler` + `SamplingMode { None, MinMax, Average, Lttb }`。设计要点：采样在**已经映射好的坐标**上做纯算术，不认识轴、series 或样式 —— 所以它能不带图表地被测（这也是 §5"什么能砍取决于标记怎么画"的落点：把"怎么画"留在外面，把"怎么选点"收进来）。MinMax 每列保留**最低与最高**两个点：一个千分之一宽的尖峰不能被平均掉，这是心电/振动这类读数存在的意义，测试就盯这一条。另外 MinMax **无条件保留首尾点** —— 对实时曲线来说"最新样本"就是全图的意义，而它未必是所在列的极值（这条是测试逼出来的：末点 1999 被同列的 1998 抢走了极值）。Lttb 用经典的最大三角形三桶法；曲线单调递增的断言保证不会画回头线。无需采样（点数 ≤ 列数）或列数小到画不出来时，返回**同一个列表实例**，调用方可以据此免去一次分配。120 个测试绿。**3.4 拆成 3.4a（本次，纯算法）与 3.4b（接进 `LineSeries<T>`：桶数来自可见像素列、点数 ≤ 像素列自动关闭、先判空后分桶）**。
- **未决（2026-10-13 提问）**：`ChartValue` 要不要改用 C# 15 的 union。**实测**：仓库 SDK 10.0.401 的 Roslyn 拒绝 `LangVersion=15.0`（CS1617），所以现在用不了（`latest` = C# 14）。**判断**：性能收益很小且打错地方 —— 当前布局 40 字节（Kind 4 + Number 8 + DateTimeOffset 16 + Category 引用 8），union 叠放负载后约 24 字节（省 40%），但热路径（映射/建几何/绘制）只在单位空间 `double` 上跑，`ChartValue` 只出现在入口一次；而真正的杠杆是待办里的"归一化缓存改单位空间 `double[]`"（每点 80 → 16 字节，好过 union 的 48）。union 真正买到的是**布局不随种类增长**（每加一种今天会让每个值变胖）+ 编译器生成的相等性/`ToString`/穷尽模式匹配（现在是手写）。**代价**：它是语言版本而非 TFM，拦的是**使用方 SDK**；且提案形状仍在变。**结论**：现在不换，等 C# 15 进入可要求的稳定 SDK 且真有加种类的需要时再回看。
- 2026-10-13 条目 3 · 步骤 3.4b 完成：采样接进 `CartesianSeries<T>`（因此所有笛卡尔系列都有，不只是折线）。`Sampling` 属性默认 `None`，改它会让该系列请求重绘；`BuildRuns` 在把值映射成坐标之后、按绘图区宽度算出桶数（一个桶 = 一个像素列）再交给 `Sampler`。**两条规则落在一处而不是散在各调用点**：(1)"点数 ≤ 可见像素列就自动不生效"由 `Sampler` 自己保证（点数够少时原样返回同一个列表）；(2)"先判空后分桶"由结构保证 —— 缺失值在 `BuildRuns` 里已经切成独立的 run，分桶只在 run **内部**发生，所以再怎么采样也不会跨着间隙画出一条线（测试专门盯这条：400 个密集点 + 一个 NaN + 400 个密集点 → 仍然是 2 个 run）。124 个测试绿。
- 2026-10-13 条目 3 · 步骤 3.5 完成：Fixed 模式的窗口语义。**这一步没有新实现** —— 语义在步骤 1.2（`AxisBase.Resolve`）里就写完了，所以本步做的是**按调用方视角把它钉死**（`FixedWindowTests`，六条）：更宽的图在同样的尺度下显示**更多数据**而不是更大间距；zoom 就是改 `PixelsPerUnit`（四倍的 px/单位 = 四分之一的可见数据）；pan 就是改 `Minimum`（窗口长度不变、位置平移）；锚点即使在 12 这种不整齐的值上也会落到网格上（12 → 10，`Interval = 5`）；窗口末尾允许残缺末格，而网格照样铺到最后一条线（末格剩余 < 一个间隔）；把图从 200 加宽到 600，间隔**不变**、线数变多。另外两条测试在写的时候暴露了我自己的算术错误（拿"上一次读到的跨度"去推断下一次，而中间有一个 4px 的刻度余量在变），改成以 `PlotBounds.Width` 直接表达期望 —— 测试读的是图表自己算出来的绘图区，而不是我对它的记忆。130 个测试绿。**条目 3 只剩 3.1b（自然月/年梯级）**。
- 2026-10-13 条目 3 · 步骤 3.1b 完成（**条目 3 收口**）：`DateTimeAxis` 的自然月/年梯级。时钟梯级之后先接"两周"（仍是整日：两周读得出是几天，25 天读不出是什么），再交给日历：1/2/3/6 个月，然后整年（1/2/5/10… 每十年一档）。刻度落在**月首**（年步落在元旦），是**按日历**走的 —— 这正是固定天数做不到的事：40 天的跨度按整日走会有线落在月中，而按日历走每条线都是月初。**标签改由步长决定**，而不是由每个时刻自身的精度决定：原来的"时刻精度"规则会让跨年的 1 月线缩成裸年（一根轴上出现 `2026` 与 `2026-07` 混排），也会让跨年的日步轴中间冒出一个孤零零的年份；现在月步一根轴上一律 `yyyy-MM`、年步一律 `yyyy`。Fixed 模式把窗口锚到月首（与 3.5 的"锚点落在网格上"同一条规则）；`DisplayOffset` 现在既决定标签**也**决定网格所在的日历（UTC 的月首在 -05:00 会读成上个月，所以两者必须同一个日历）；日历步不画次级网格线（一个月的五分之一不是任何有意义的时间单位）。**实现形状**：把"格距"从一个 double 提升为 `AxisStep`（公开抽象；`EvenStep` 是等距那种；`DateTimeAxis` 私有一个 `CalendarStep`），由 `AxisBase.ResolveStep`（新的 protected 虚方法，即 §6"梯级策略可被自定义轴覆盖"的落点）产出，随 `AxisLayout.MajorStep` 传给渲染器，渲染器再用 `IAxis.FormatValue(unit, step)` 要标签。**这是新增而不是改签名**：`FormatValue(unit, step)` 带默认实现（默认忽略步长），既有自定义轴与既有的 `FormatValue(unit)` 都不受影响；`NiceInterval` 仍是"同一套等距梯级、换一组好看的数"的钩子。`DateTimeAxis` 不再需要自己的 `NiceInterval`（超过一周后的整日回落被日历梯级取代；`TimeSpanAxis` 保留整日 —— 时长没有日历可讲）。数值上钉住一条并**实测**：月首穿过单位空间（double 毫秒，2026 年是 1.79×10¹² 这个量级）必须原样回来，否则一条线会被读成"上个月"并照上个月的标签画（1970–2400 共 5160 个月首、36525 个午夜，**与 0 误差**；原因是整日的时间计数是 2 的幂的倍数，正好落在 double 能精确表示的那些值上），这条有测试守着。141 个测试绿。**条目 3 完成**。**待用户决策**：`AxisStep`/`EvenStep` 两个新公共类型，以及 `AxisLayout.MajorInterval`（double，位置参数）→ `AxisLayout.MajorStep`（`AxisStep`，位置参数；`MajorInterval` 保留为只读派生属性，读的地方全不受影响）。这一处是本次唯一改动既有形状的地方，只影响**自己构造** `AxisLayout` 的代码（自定义轴的实现），理由是：若保留 double 就只能在"把日历概念塞进中立基类"和"用一个数字偷偷编码月份数"之间选，两者都比一个说得出名字的步长差。
- 2026-10-14 条目 4 · 步骤 4.1 完成：主题令牌与 `ControlTheme`。**令牌全集**（§7 那张表一行不落）成为 `AvCharts.Themes.ChartTokens` 里的公共常量 —— 键名是公共 API 并被冻结，因为宿主正是靠它覆盖颜色的。类型按 §7 的规矩：填充类 `IBrush`（背景、文字、标记内部）、描边类 `Pen`（轴、网格、标记边缘），`Pen` 让虚线（十字线默认就是虚线）与渐变免费。**值**：浅色一套沿用 `tools/palette-preview.py` 里那份已经预览过并定稿的数（surface `#F9F9F9`、主网格 `#CFCFCF`、次网格同色 @47%、刻度字 `#333333`、轴线 `#333333`@60%、标题 `#777777`、轴带 `#AAAAAA`@3.5%、图例片 `#333333`@20%）；深色单独一组（surface `#1C1C1E` —— §13 那条未决项到此关闭；主网格 `#323539`、次网格 `#232426`、刻度字 `#A6A7AC`、轴线 `#5A5B5B`、标题 `#C8C7C3`、轴带 `#202123`@88%、图例 `#26272B`）；两套的前 10 个系列色就是定稿的那两个色板，fill = 同色 @47%（0x77）。文档没给的那些（焦点环、标记的悬停/选中、十字线、提示、涨跌、选择区）按同一口径补：焦点环取色板里的蓝（light `#209FD9` / dark `#4CB6E8`），悬停与选中**共用这一个强调色、只差描边粗细**（不为"有第二种交互色"而发明一种颜色），涨跌一组绿/红，选择区与焦点环同色而更淡。**机制**：`ChartTheme.axaml` 是一套 `ThemeDictionaries`（Light/Dark）加一个按类型做键的 `ControlTheme`；`ChartTheme.Source`（XAML `ResourceInclude` 用）与 `Create()`（代码里合并用）是宿主的两个入口。`ControlTheme` 把 8 个令牌落在 `ChartBase` 的属性上（surface 三件套 + 轴线/刻度/网格/次网格/标签刷），图表再把轴的那 5 个传给图层：`ApplyAxisInks` 在**属性变化时**与**图层后到时**各跑一次，`AffectsRender` 让 surface 自己重绘。**这轮踩到并记下的两件事**：(1) `ControlTheme` 在 Avalonia 里只能经由 **style host** 送达控件，而颜色主题的宿主接法（§7 承诺的"用 Resources 覆盖任意令牌"）是资源字典 —— 所以 `ChartBase` 在挂进逻辑树时**自己按类型去资源里找主题**（先看自己的类型，再逐级向上到 `ChartBase`，最后兜底问 `Application.Resources`）。这样宿主一行 `ResourceInclude` 就够，而 Avalonia 自己的样式路径依旧优先（`Theme` 已被设过就不动）。(2) `<Styles>` 根的 axaml **无法**用 `AvaloniaXamlLoader.Load(uri)` 在运行时加载，并且经**项目引用**（而非 NuGet）引用一个带编译 XAML 的库时，`StyleInclude` 会在编译期失败 —— 引用程序集里没有生成的 `!AvaloniaResources`。所以主题做成**资源字典**而不是 Styles 文件：Demo 与将来的宿主都能一行接上，NuGet 宿主也不受影响。另外新增 `AxisLayer.TickPen`（刻度线可以有自己的笔，不设则跟随轴线），并把 Demo 里手写的轴笔删掉 —— 它现在穿的是主题。147 个测试绿（新增 6 条令牌契约测试：每个键在两个变体里都有值；按笔/刷分好类且分类覆盖全部键；调色板 10 对且 fill 与 stroke 同色 @47%；`ControlTheme` 真的给图表穿上并把轴笔传到图层；宿主覆盖单个令牌即换皮；主题能按宿主那种 `ResourceInclude` 方式加载）。
