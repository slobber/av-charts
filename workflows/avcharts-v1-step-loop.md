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

- [ ] 步骤 2.1 绘制内核：layer 模型、`SeriesRenderContext`、批量几何、设备像素吸附、`InvalidateVisual` 粒度
- [ ] 步骤 2.2 `Series<T>` 四阶段钩子 + `LineSeries<T>` + `IMarkerRenderer`（圆/方起步）
- [ ] 步骤 2.3 `ChartBase : Control` / `CartesianChart` + `AxisRenderer`（量文字、gutter、标签抽稀）
- [ ] 步骤 2.4 画廊里画出第一条真实折线（硬编码数据）→ **第一张金图**

### 条目 3 · 轴族补全

- [ ] 步骤 3.1 `DateTimeAxis`（日历感知梯级）
- [ ] 步骤 3.2 `TimeSpanAxis`、`LogAxis`
- [ ] 步骤 3.3 `CategoryAxis`（槽位语义 + Fixed 模式下空槽只画网格）
- [ ] 步骤 3.4 采样：`None/MinMax/Lttb/Average`、分桶语义（先判空后分桶）、点数≤像素列自动关闭
- [ ] 步骤 3.5 Fixed 窗口推导：锚点对齐 `Interval`、网格铺满、边缘残格、zoom 改 `PixelsPerUnit`、pan 改 `Minimum`

### 条目 4 · 主题与令牌

- [ ] 步骤 4.1 令牌全集（`IBrush`/`Pen` 类型）+ Light/Dark 资源字典 + `ControlTheme`
- [ ] 步骤 4.2 调色板分配语义（按声明顺序取槽、显式设色不占位、饼图按点索引）+ `ThemeVariant` 切换重绘
- [ ] 步骤 4.3 画廊 light/dark 同屏对照页 + 色觉可分辨性检查

### 条目 5 · 数据管道

- [ ] 步骤 5.1 `ItemsSource`/选择器/`Filter`/归一化值数组/`INotifyCollectionChanged` 增量更新
- [ ] 步骤 5.2 线程模型：数据只在 UI 线程 + 后台推送按帧合批入口（实时场景）
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
