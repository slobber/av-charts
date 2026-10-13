# 工作流：AvCharts v1 逐步推进（step loop）

> 状态：**生效中**（2026-10-13 定稿）。触发方式是挂在本线程上的 30 分钟心跳。
> 目标文档：`docs/design-brief.md`（每次运行的第一件事就是读它）。

## 这个 loop 是什么

一件反复发生的事：**按 `docs/design-brief.md` 把 v1 范围内的条目一条条实现完**。每一轮运行 = 往前推一个"步骤"，若干个步骤组成一个"条目"，每个条目完成时给用户一份 brief。

## 触发

- **心跳**（Codex 桌面版原生机制，挂在当前线程上），周期 **30 分钟**：上一轮还没跑完就跳过这一轮，不叠加。
- 手动：用户说"下一步"时也走同一套流程跑一轮。
- 无事可做时（v1 待办清空）→ 发一次收工 brief 并把心跳**暂停**（不归档线程，用户可能还要用它）。

## 状态（这一轮从哪继续）

- 待办清单就是本文的「待办」一节：**顺序即执行顺序**，取第一个未勾选项；
- 步骤完成时，在**同一个提交**里勾掉它，并在 `docs/design-brief.md` §14 加一行进度；
- 不做额外状态文件——文档即状态，谁也骗不了谁。

## 一轮运行的固定动作

1. 读 `docs/design-brief.md`（尤其 §1 契约、§10 矩阵、§14 进度）与本文待办，取下一个未完成步骤；
2. 实现它：代码 + XML 注释写全 + `PublicAPI.Unshipped.txt` 条目 + 覆盖语义边界的单测（空值、退化输入、边界值必测）；
3. 验证：`dotnet build -c Release`、`dotnet test`、`dotnet format --verify-no-changes` 三绿；该步骤另外要求的专项验证（金图、契约测试、性能基准）也跑；
4. 提交（**一步一提交**，信息说明改了什么、为什么），推送；
5. 若这一步所在**条目**完成 → 发 brief 给用户；否则静默继续，若还有余量就接着做下一步（一轮只受"条目"边界约束，不受 30 分钟约束）。

## brief 的样子（检查点的唯一产物）

一个条目完成时，用一条消息说清：做了什么、验证结论（构建/测试数/格式/包版本）、下一步是什么、有没有需要用户决策的点、以及**关键文件的链接**。不给新东西的原始输出，用户读结论不读草稿。

## 什么时候叫醒用户（只有这四种）

1. 一个 v1 条目完成（brief）；
2. v1 待办清空（收工 brief + 暂停心跳）；
3. **同一个阻塞连续出现三次** → 标记 blocked 并停下等（按 goal 的规则，不硬撑）；
4. `docs/design-brief.md` 的**冻结契约**与实现对不上（见下）。

其余一律静默——不做进度播报。

## 自主边界（无人值守时不许越的线）

- **设计文档是契约，不是期刊**：§1 程序集边界、§6 轴语义、§7 令牌集、§9 扩展契约形状、§11 性能指标，实现时发现它们有问题 → **停下问用户**，不许自行改文档或自行偏离；
- 不发布、不打 tag、不推私有源（发版由用户决定）；
- 不动仓库凭据、不动 `nuget.config` 的源映射；
- 不为了让测试过而放宽验证：不删测试、不调阈值、不跳过金图；
- 不引入与 design-brief 冲突的新公共 API 名词（想加就先问）。

## 待办（v1；顺序即执行顺序；一个「条目」= 一个检查点）

### 条目 1 · 数据变换与线性轴（依赖最深的两个，先钉死）

- [x] 步骤 1.1 `DataTransform`：维度注册、值↔屏幕映射、`DescribeData` 的值域聚合、空值跳过、退化值域回落（25 个测试绿）
- [x] 步骤 1.2 `AxisBase`/`LinearAxis`：nice 梯级（1/2/2.5/5）、`Interval = null` 的自动吸附、`PixelsPerUnit` 与 `AxisScaleMode`、`Separators`（41 个测试绿；轴改成纯计算、结果归 transform，共享轴实例成立）

### 条目 2 · 最早的可视画面（走通一根线，验证整套架构）

- [x] 步骤 2.1 绘制内核：layer 模型、`SeriesRenderContext`、批量几何、设备像素吸附、`InvalidateVisual` 粒度（53 个测试绿；图层的重绘粒度由步骤 2.4 的金图验证）
- [x] 步骤 2.2 `Series`/`CartesianSeries<T>`/`LineSeries<T>` 四阶段钩子 + `IMarkerRenderer` + `SeriesLayer`（63 个测试绿，含一条真实出墨的渲染断言）
- [x] 步骤 2.3a `AxisRenderer`（模型侧）：gutter 量测、刻度位置与标签文本、标签抽稀（抽稀沿轴方向判断而非屏幕方向，两端优先保留）（72 个测试绿）
- [x] 步骤 2.3b 图层与控件接线：`AxisLayer` 画网格与轴家具、`ChartBase:Control` / `CartesianChart` 的量测与布局（绘图区 = 控件边界减去 gutter，迭代到收敛）（79 个测试绿；"视觉树真的会渲染这些图层"留给 2.4 的金图证明）
- [x] 步骤 2.4 画廊里画出第一条真实折线（硬编码数据）→ **第一张金图**（82 个测试绿；金图基础设施：`AVCHARTS_UPDATE_GOLDENS=1` 录制、`Golden/Received` 存差异图、0.1% 像素容差、字体钉死 Inter）

### 条目 3 · 轴族补全

- [x] 步骤 3.1 `DateTimeAxis`：单位空间是 1970 起的毫秒（ULP ≈ 240ns，10µs 步长有 40 倍余量），梯级从 10µs 起按钟表步长走到秒/分/时/天，超过一周按整日步进；标签按步长自适应精度；时刻用 `DateTimeOffset`（承载偏移、按 `UtcTicks` 排序、`DisplayOffset` 决定标签用的钟面）；93 个测试绿，含"10µs 步长误差 10ns"与"1µs 步长仍可分辨"
- [x] 步骤 3.1b DateTimeAxis 的自然月/年梯级：一周之后先接"两周"（仍是整日），再交给日历 1/2/3/6 个月、然后整年（1/2/5/10…，每十年一档）；刻度落在月首、年步落在元旦；标签由**步长**决定（月步一律 `yyyy-MM`、年步一律 `yyyy`），不再由每个时刻自身的精度决定；Fixed 模式锚到月首；`DisplayOffset` 同时决定网格与标签所在的日历；日历步不画次级网格线。形状上把"格距"从 double 提升为 `AxisStep`/`EvenStep` + 轴私有的 `CalendarStep`，经 `AxisBase.ResolveStep`（protected 虚方法，即 §6"梯级策略可被自定义轴覆盖"的落点）产出、随 `AxisLayout.MajorStep` 交给渲染器，标签走 `IAxis.FormatValue(unit, step)`（默认忽略步长，所以既有自定义轴不受影响）（141 个测试绿）
- [x] 步骤 3.2a `TimeSpanAxis`：时长是毫秒数（`ChartValue` 没有时长种类，选择器写 `item.Elapsed.TotalMilliseconds` 最自然）；梯级与标签复用钟表规则（抽成 `ClockLadder`，`DateTimeAxis` 也改用它）；标签形如 `01:02:03.456`、跨天写 `3.01:02:03`；没给 `Interval` 时按数值自身精度决定小数位（99 个测试绿）
- [x] 步骤 3.2b LogAxis：单位空间是十进制指数（100 → 2、0.1 → -1），0 与负值视为缺失而不被挪到某处；刻度落在 10 的幂上，间隔取 1/2/5 个数量级，细于一个数量级不给（亚数量级的 1-2-5 内刻度需要非均匀刻度生成，已记档）（105 个测试绿）
- [x] 步骤 3.3 `CategoryAxis`：单位空间是槽位号，槽位按类别首次出现的顺序发放（`ClearCategories` 重来）；标签是类别名（不在槽位上时给数字而不是错的名字）；刻度落在整槽（间隔取整）；半个槽位的端部余量属于带宽布局（条目 6 第 6.1 步）（112 个测试绿）
- [x] 步骤 3.4a `Sampler`：`SamplingMode { None, MinMax, Average, Lttb }`，在**已映射的坐标**上做纯算术（不碰轴/series/样式，所以可无图表测试）；MinMax 每列保留最低与最高（峰值不可被平均掉）并**始终保留首尾点**（实时曲线的最新样本必须有）；Lttb 按最大三角形选点；点数不足或不需采样时原样返回同一个列表（120 个测试绿）
- [x] 步骤 3.4b 采样接进 `CartesianSeries<T>`：`Sampling` 属性（默认 `None`）、桶数 = 绘图区像素列数、点数 ≤ 列数时自动不生效、分桶只在 run 内部（间隙两侧永不连出线）（124 个测试绿）
- [x] 步骤 3\.5 Fixed 窗口推导：语义在步骤 1.2 就实现了（锚点对齐 `Interval`、网格铺满窗口、末格可残缺、zoom 改 `PixelsPerUnit`、pan 改 `Minimum`），本步把它按调用方视角钉死：更宽的图显示更多数据而不是更大间距、zoom 换算成立、pan 只移窗口不改长度、锚点落在网格上、末格残缺且网格仍铺到边（130 个测试绿）

### 条目 4 · 主题与令牌

- [x] 步骤 4.1 令牌全集（`IBrush`/`Pen` 类型）+ Light/Dark 资源字典 + `ControlTheme`：53 个令牌键成为公共常量（`AvCharts.Themes.ChartTokens`），`ChartTheme.axaml` 里一套 Light/Dark `ThemeDictionaries`（浅色沿用 SciChart 实测值，深色单独一组），`ChartTheme.Source`/`Create()` 给宿主 XAML 与代码两条接法；`ControlTheme` 把 8 个令牌接到 `ChartBase` 的属性上，图表再把轴的那几个传给图层（147 个测试绿）
- [x] 步骤 4.2 调色板分配语义（按声明顺序取槽、显式设色不占位、饼图按点索引）+ `ThemeVariant` 切换重绘：`Series` 多了 `PaletteStroke`/`PaletteFill`/`HasOwnColours`（模型层只知道"图表给我的颜色"，不认识主题）；图表按声明顺序发槽、显式设色的系列不占槽、槽位循环，`PaletteStroke(index)`/`PaletteFill(index)` 供按点取色；变体或资源变化时重读调色板并重绘（同图层同几何，只换颜色）（152 个测试绿）
- [x] 步骤 4.3 画廊 light/dark 同屏对照页 + 色觉可分辨性检查：Demo 用两个 `ThemeVariantScope` 在同一窗口并排画十色板（light | dark，同一份数据）；`tools/palette-vision.py` 直接**从主题文件**读色板做 deuteranopia 模拟（Machado 1999）与 CIE76 距离、对比度检查并出对照图。**结论**：相邻槽位在模拟下仍分得开（light 24.7 / dark 11.1 dE），但非相邻两两会塌（light 2↔10 仅 1.6、dark 2↔10 3.1）—— 十色板对红绿色盲的固有限制，不是这十支颜色挑得不好；浅色最弱对比度 2.27（slot 3 teal），dark 最弱 5.18。**待用户决策**：颜色之外的身份通道（虚线样式/标记形状/图例）不在本步范围内，登记待议（153 个测试绿）

### 条目 5 · 数据管道

- [x] 步骤 5.1 `ItemsSource`/选择器/`Filter`/归一化值数组/`INotifyCollectionChanged` 增量更新：归一化缓存改成**单位空间的 `double[]` 两数组**（每点常驻 16 字节；3.1 记档的那条在此收口），`XValues`/`YValues` 给子类、`DescribeData` 直接把数组交给 `DataDescription`（只取最低/最高，一个点不再产生一个 `BoundedValue`）；默认横坐标是**保留下来的记录里的位置**（被 `Filter` 丢掉的记录**不留槽位**，与"空值保留槽位"是两回事，两处文档都写死）；`INotifyCollectionChanged`：**末尾追加**与**原地替换**按增量走，中间插入/删除/`Reset` 一律重读源（横坐标会整体位移），条目自身 `INotifyPropertyChanged` 也跟（重读源）；换轴（单位空间变了）时自动重读；`InvalidateData` 转为**公开**（宿主的数据源不广播时的入口）（163 个测试绿）
- [x] 步骤 5.2 线程模型：数据只在 UI 线程 + 后台推送按帧合批入口。`Series.Push`（任意线程、加锁缓冲）+ `Series.ApplyPushed`（虚方法，UI 线程上批量应用）+ `Series.Pushed` 事件（在**推送线程**上抛出，只用来请求一次 UI 线程的 pass）；`CartesianChart` 每次 `ArrangeOverride` 前排空一次、另外用一个合并过的 `Dispatcher.UIThread.Post` 唤醒空闲图表；一批 100 个点**只触发一次 `Invalidated`**（"避免每点一次 invalidate"有测试钉住）；推入的记录会**进入 `ItemsSource`**（源与值不分家），因此可推的前提是源本身可加（`ICollection<T>` 且非只读，数组会在 `Push` 处当场报错而不是丢数据）（170 个测试绿）
- [ ] 步骤 5.3 空值语义 `NullValueMode { Gap, Connect, Zero }`（默认 Gap）+ 采样/值域/堆叠里的空值规则
- [ ] 步骤 5.4 命中测试：空间索引、`FindDataPoints`/`GetAllDataPoints`/`GetItemsInRegion`、`MouseSensitivity`、`HitData`、`ChartItem` 惰性物化

### 条目 6 · 其余 v1 系列

- [ ] 步骤 6.1 Column/Bar：带宽布局、`SeriesLayout { Cluster, Overlay, Stacked }`、`columnWidthRatio`/`maxColumnWidth`
- [ ] 步骤 6.2 Area / Plot / Bubble / StepLine / StepArea
- [ ] 步骤 6.3 每系列轴（双 Y 轴）+ 共享轴实例的锁定测试

### 条目 7 · 交互

- [ ] 步骤 7.1 视窗 API：`PanBy`/`ZoomBy`、`DataToPoint`/`PointToData`、`VisibleRangeChanged`
- [ ] 步骤 7.2 pan/zoom 手势（滚轮缩放锚指针、拖拽平移、Shift/Ctrl 单轴）
- [ ] 步骤 7.3 十字线 + data tip（含 `TipStack` 防重叠）
- [ ] 步骤 7.4 选择（单选/多选 + 键盘 Ctrl/Shift 语义）+ 图例点击切换可见性
- [ ] 步骤 7.5 事件 + `ICommand` 属性

### 条目 8 · 扩展契约与验收

- [ ] 步骤 8.1 S1–S4 落地 + 钩子顺序契约测试 + `DataTransform` 可注入
- [ ] 步骤 8.2 画廊补全（每特性一页、ECG 页：Fixed 尺度 + 等格 + 采样 + 空值断口）+ 金图基准铺开
- [ ] 步骤 8.3 性能验收：数学层 BenchmarkDotNet + §11 那张表逐条实测
- [ ] 步骤 8.4 v1 验收演练（组合图/双 Y/时间轴/堆叠柱/十万点/light-dark/data tip/十字线/图例/ECG）
- [ ] 步骤 8.5 文档收口：英文 XML 注释复核、README 定稿、docfx 站点产出

## 定稿时的四条约定

1. **顺序**：最小数学 → 尽早出画面 → 再补全轴族/采样。理由是架构风险暴露的时机——一条真实的线会一次性压到 layer 划分、失效粒度、gutter 文字量测、主题取色这五件事的耦合上，那是最可能出错也最该早发现的地方。
2. **文档与实现冲突**：停下问，不自行判断后记档。契约是契约。
3. **提交落点**：直接推 `master`（私有库 + 单工作者）；要退回某一步就是一次 `git revert`。
4. **心跳周期**：30 分钟，上一轮未完成则跳过，不叠加。

## 换线程 / 重开时的启动指令

上下文快满时换一个新线程，什么都不会丢 —— 状态在盘上：本文的待办清单是"下一步做什么"，
`docs/design-brief.md` §14 是"已经做了什么"，git 历史是"为什么"。新线程只要照下面两段做。

**一、给新线程的第一句话**（把仓库文件夹指向 `E:\GitHub\slobber\av-charts`）：

> 继续推进 AvCharts 的 v1：读 `workflows/avcharts-v1-step-loop.md` 的待办与 `docs/design-brief.md` §14，
> 按同样的规矩一步一提交地往下做，并在完成后重建这个心跳自动化（提示词见本文件下一节）。

**二、要重建的心跳**（kind=heartbeat、destination=thread、rrule=`FREQ=MINUTELY;INTERVAL=30`、status=ACTIVE、name=`AvCharts v1 step loop`），
提示词照抄：

> 继续推进 AvCharts 的 v1，一次一个步骤（工作目录 E:\GitHub\slobber\av-charts）。
>
> 做法：
> 1. 读 docs/design-brief.md（尤其 §1 契约、§10 矩阵、§14 进度）与 workflows/avcharts-v1-step-loop.md 的待办，取第一个未勾选的步骤。
> 2. 实现它：代码 + XML 注释写全 + PublicAPI.Unshipped.txt 条目 + 单测（空值、退化输入、边界值必测）。
> 3. 验证：dotnet build -c Release、dotnet test、dotnet format --verify-no-changes 三绿；该步骤另有要求的专项验证（金图、契约测试、性能基准）也跑。
> 4. 一步一提交（提交信息说明改了什么、为什么）并推 origin master；同一次提交里勾掉待办，并在 design-brief §14 记一行进度。
> 5. 一轮尽量把一个「条目」里的步骤做完；条目完成即停并发 brief。
>
> 只在下面四种情况给用户发消息，其余时间保持静默、不要发进度播报：（1）一个 v1 条目完成 —— 发 brief：做了什么、验证结论（构建/测试数/格式/包版本）、下一步、需要用户决策的点、关键文件链接；（2）v1 待办清空 —— 发收工 brief 并把本心跳置为暂停（不要归档线程）；（3）同一个阻塞连续三次 —— 标记 blocked 停下；（4）docs/design-brief.md 里已冻结的契约（§1 程序集边界、§6 轴语义、§7 令牌集、§9 扩展契约形状、§11 性能指标）与实现对不上 —— 停下问。
>
> 硬边界：不发布、不打 tag、不推私有源、不动仓库凭据与 nuget.config 的源映射、不为了让测试通过而删测试或调阈值。
>
> 会话层注意：应用工作目录可能仍指向改名前的 E:\GitHub\slobber\av-chart（不存在），编辑文件要用 `../av-charts/<路径>` 落到真实仓库，命令行显式指定工作目录；PowerShell 的 `Remove-Item` 带 `-Force` 会被策略拦，不带就过。

**三、换线程之后**：把旧线程上这个心跳删掉或暂停（否则两个线程会同时动手改同一个仓库）。

## 本线程落下的经验（新线程开工前值得知道的）

- **PowerShell 反引号坑**：用双引号字符串写文档时，`` ` `` 会被当转义吃掉（我因此在规格文件里丢过两次反引号，搜不到那一行）。写这类替换用单引号，或按行正则替换。
- **PublicAPI 声明文件**：新增公共 API 会让构建报 RS0016 并逐条列出需要的行；用脚本从编译输出里抽取（`符号“(.+)”不是已声明`）再写回文件，别手抄。反过来删掉 API 会报 RS0017，需要手工删行。
- **`Bitmap.Save(string)` 在 Avalonia 12 已过时**，要传 `new PngBitmapEncoderOptions()`。
- **`Avalonia.Headless.XUnit` 在 12.x 依赖 xunit v3**，与房子的 xunit 2.9.3 冲突（`FactAttribute` 双来源）：用纯 `Avalonia.Headless` + 手工 `SetupWithoutStarting()`。
- **`VisualChildrenCount`/`GetVisualChild` 在 Avalonia 12 不可重写**，用 `VisualChildren.Add(...)`。
