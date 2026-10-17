# 交互

读者面对一张图会做四件事：指着看、把窗口挪到别处、把某个读数挑出来、把一段放大。这一页讲这四件事各自
对应什么、报出来什么，以及怎么接。

## 命中测试

```csharp
IReadOnlyList<HitData> hits = chart.FindDataPoints(point);   // 每个系列一条，按距离排序
HitData? nearest = hits.Count > 0 ? hits[0] : null;
```

命中回来的 `HitData` 上什么都有：`Distance`（多远，逻辑像素）、`ChartItem`（哪个记录）、`Item`（你自己的
对象）、`X`/`Y`（数据空间的值）、`Position`（画在哪）、`DisplayText`（提示会写的那行字）、`SeriesColor`
（系列的颜色，提示和图例旁边那一小块用它）。

几个入口：

| 方法 | 给什么 |
|---|---|
| `FindDataPoints(point)` | 每个系列里离指针最近的一条，按距离排序 |
| `FindDataPoints(point, sensitivity)` | 同上，但用一个指定的容差 |
| `GetAllDataPoints()` | 屏幕上所有画出来的记录（采样之后的） |
| `GetItemsInRegion(rect)` | 一个矩形范围内的记录 |

`MouseSensitivity` 默认 5 逻辑像素。**能看见才打得中**：缺值的记录永远不是命中（没有东西可指），
`Zero` 模式画出来的点可以是；柱、条、饼按形状命中（在矩形/楔形里距离算 0），线按线段距离。

## 数据提示

```csharp
chart.DataTipMode = DataTipMode.Item;      // 默认：贴着那个读数
chart.DataTipMode = DataTipMode.Mouse;     // 跟着指针走
chart.DataTipMode = DataTipMode.None;      // 关掉
```

默认就是 `Item` —— v1 的承诺是"数据提示是开着的"。三件附带的事：

- **`ShowAllDataTips = true`**：屏幕上每个读数都挂一张，**堆叠起来互不遮挡**（Flex 的 `TipStack`，这里也是
  同一个机制）。它是给"一屏只有几个读数"的图准备的：一个十万点的系列会想要十万张提示，那不是它该干的
  事。
- **`DataTipFunction`**：`Func<HitData, string>`，换掉那行字。不给就写轴的格式化结果（每个维度用**它自己
  那根轴**写，所以第二把尺的提示用的是第二把尺的刻度），这就是 Flex 的 `dataTipFunction`。
- **`DataTip` 是模板控件**：想要另一种样子的框，就写一个 `ControlTheme` 给 `DataTip`（模板里读 `Data` 与
  `Text` 两个属性）。提示是图表视觉树里的真控件。

## 十字线

```csharp
chart.ShowCrosshair = true;            // 默认关
HitData? where = chart.CrosshairHit;   // 现在指着哪个读数
```

十字线**吸附到全图最近的有效点**（不理会 5px 的指针容差 —— 吸附不是命中），两个牌子上的读数由**那个点
所属系列的轴**写出来，所以两根纵轴的图各自用自己的刻度。牌子放得进 gutter 就贴着轴放，放不下就挪进绘图区，
整块始终留在图内。

## 选择

```csharp
chart.SelectionMode = ChartSelectionMode.Multiple;
int count = chart.SelectedItems.Count;      // 选中的记录（ChartItem 视图）
ChartItem? caret = chart.Caret;             // 光标所在的记录
```

默认 `None`，因为**选择会让图表要键盘**：一个没被要求就抢走键盘的图表，是挡在宿主的输入框前面的图表。
要了选择，图表就变成可聚焦的，焦点环也真的画出来；把选择关掉会放开已经选中的东西。

指针语义（和文件列表一样）：

| 操作 | 结果 |
|---|---|
| 单击一个读数 | 选中它（`Single` 下替换原来的） |
| `Ctrl` + 单击 | 切换这一个选中与否 |
| `Shift` + 单击 | 从锚点取一段 |
| 点空白处 | 放开选择 |
| `Alt` + 拖拽 | 框选：把这一段里的记录挑出来（画一个带子） |

键盘语义：

| 键 | 结果 |
|---|---|
| `←` `→` | 同一个系列上前/后一个记录 |
| `↑` `↓` | 换到上/下一个系列 |
| `Home` / `End` | 跳到这一系列的两端 |
| `Space` | 把光标所在的记录选中 |
| 配 `Ctrl` | **只移动光标，不改选择** |
| 配 `Shift` | 从锚点取一段 |

到端点不绕回来。`SelectionChanged` **只在选择真的变了**时报：重复点同一条不报，`Ctrl` 只移光标也不报。

## 事件与命令

```csharp
chart.ItemClick += (_, e) => details.Text = $"{e.Item}";
chart.SelectionChanged += (_, e) => status.Text = $"{e.Selection.Count} selected";
chart.ItemDoubleClickCommand = openCommand;    // 一个 ICommand：双击时执行，参数是 ChartItemEventArgs
```

| 事件 | 什么时候 |
|---|---|
| `ItemPointerEntered` / `Exited` / `Moved` | 指针进出一个记录的感应范围、在里面移动 |
| `ItemPointerPressed` / `Released` | 在一个记录上按下、抬起 |
| `ItemClick` / `ItemDoubleClick` | 点一下 / 点两下 |
| `SelectionChanged` | 选择真的变了 |

参数是 `ChartItemEventArgs`：`Hit`（命中详情）、`ChartItem`、`Item`（你自己的对象）、`Selection`（图表自己
那份现成视图，**不是副本** —— 要留住当下的样子就自己复制一份）。给视图模型准备的是三个命令：
`ItemClickCommand`、`ItemDoubleClickCommand`、`SelectionChangedCommand`；指针级的六个事件只给视图，
不给命令。

三件要知道的事：

- **数据点不是屏幕上的东西**：它背后没有一个 `Visual`，没有可以订阅的对象。所以"每个记录的事件"是图表
  按命中测试的结果**自己合成**的，`sender` 是图表本身。
- **拖拽不算点击**：按下和抬起照报（`Pressed` / `Released`），`Click` 不报。
- **双击由平台给的 `ClickCount` 决定**，图表不自己计时。

## 滚动条与概览

窗口不只有手势一条路。两个控件都能接在一张图上：

```csharp
var under = new ChartScrollBar { Chart = chart };
var beside = new ChartScrollBar { Chart = chart, Orientation = Orientation.Vertical };
var whole = new ChartOverview { Chart = chart };
```

| 控件 | 做什么 | 关键属性 |
|---|---|---|
| `ChartScrollBar` | 轨道画在绘图区自己那一段上；滑块的长度 = 屏幕上占读数的份额，位置 = 窗口在读数里的位置。拖滑块平移窗口，点轨道把窗口挪到指针处。两端没有箭头按钮，默认只有 8px 粗 | `Chart`、`Orientation`、`MinThumbLength`（默认 16）、只读的 `CanScroll` |
| `ChartOverview` | 把**整段**读数画成缩略形状（每条系列一条，缺值断开），窗口是躺在上面的一块：拖中间平移，拖两端缩放，点别处跳过去 | `Chart`、`Orientation`、`WindowFill`、`WindowPen`、只读的 `CanScroll` |

两者都只是"把窗口推到别处"，推的是同一个窗口 —— 所以用滚动条拖过的图，和用滚轮缩过的图，是同一张图。
画廊的 Scrolling 页把它们和图表一起放进一个 `Grid`，可以直接抄。

## 摸得着的入口都是 protected

指针、滚轮、键盘的处理函数是薄壳，规则落在 protected 方法上，宿主自己接触摸板、笔、远端输入时走同一条
路：`WheelZoom(notches, at, modifiers)`、`DragStart` / `DragMove` / `DragEnd`、`SelectAt(at, modifiers)`、
`BandStart` / `BandMove` / `BandEnd`、`TrackPointer(at)`、`PressAt` / `ReleaseAt`、`MoveCrosshair` /
`HideCrosshair`、`ShowDataTipsAt` / `HideDataTips`、`Navigate(key, modifiers)`。

---

[English version](../en/using/interaction.md)
