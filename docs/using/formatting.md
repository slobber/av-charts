# 图例、元素与导出

颜色主题、资源令牌、Light/Dark 和 Palette 的完整说明已经移到[主题与调色板](theming.md)。这一页讲颜色之外
的几件事：图例、画在图表上的元素、导出，以及控件外面的留白。

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

图例色块读取系列的 `Color`，所以系列自己的颜色、Palette 槽位和提示旁的色块会保持一致。怎样配置 Palette
或单系列颜色见[主题与调色板](theming.md#系列自己的颜色)。

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
