namespace AvCharts.Demo;

/// <summary>The demo's words in the languages it speaks, keyed by the English.</summary>
/// <remarks>
/// <para>
/// Keyed by the English rather than by a name of its own, because the English is what the pages already say: a
/// page writes the word a reader sees, and a translation is found by looking that word up. Nothing has to be
/// invented for a string that is not here yet - it is simply shown in English until somebody writes it down -
/// and a word that changes in a page stops being translated rather than being translated wrongly.
/// </para>
/// <para>
/// Only what a reader reads is here: a page's name, its line, the name of an example and the words on a knob.
/// The readings an example is drawn from are not translated, because they are the example's data rather than the
/// demo's words - a chart of sales by region says North and South in either language, the way the sales do.
/// </para>
/// </remarks>
internal static class Translations
{
    /// <summary>What the demo's words are in Chinese.</summary>
    public static readonly Dictionary<string, string> Chinese = new(StringComparer.Ordinal)
    {
        // 目录：分组的名字。
        ["Charts"] = "图表",
        ["Live data"] = "实时数据",
        ["Axes and scales"] = "轴与尺度",
        ["Theme and interaction"] = "主题与交互",

        // 目录：每一页的名字与一句话。
        ["Everything at once"] = "一页看全",
        ["The acceptance list on one page, and a hundred thousand readings."] = "验收清单挤在一页上，外加十万个读数。",
        ["Lines"] = "折线",
        ["A line, one with markers, an area under one, and two step lines."] = "一条折线、一条带标记的、一条带面积的，还有两条阶梯线。",
        ["Columns"] = "柱形",
        ["Columns clustered, stacked, and with a line over them."] = "柱形：并排、堆叠，以及上面压一条线。",
        ["Bars"] = "条形",
        ["The same kind of thing lying down, against categories."] = "同样的东西躺下来，对着类别摆放。",
        ["Points"] = "散点",
        ["A scatter of readings, and a bubble sized by a third reading."] = "一片散布的读数，以及由第三个读数决定大小的气泡。",
        ["Candles"] = "蜡烛",
        ["A price a session: a body from the open to the close, a wick from the low to the high."] = "每个交易日一根价格：实体从开盘到收盘，影线从最低到最高。",
        ["Pie"] = "饼图",
        ["A whole divided into shares: one wedge per reading, sized by what it is worth."] = "把一个整体分成若干份：一个读数一块，大小由它占的份额决定。",
        ["Streaming"] = "流式",
        ["A thousand readings a second, arriving while the chart draws."] = "每秒一千个读数，在图还在画的时候不断到达。",
        ["Oscilloscope"] = "示波器",
        ["A triggered sweep of a live signal, with a timebase and a gain."] = "对实时信号做触发扫描，带时基与增益。",
        ["Axes"] = "坐标轴",
        ["Linear, logarithmic, time, duration and categorical scales."] = "线性、对数、时刻、时长与分类五种尺度。",
        ["Two scales"] = "双尺度",
        ["Two vertical scales on one plot, with a legend beside it."] = "一张图两条纵轴，旁边有图例说明哪条是哪条。",
        ["ECG"] = "心电",
        ["A fixed scale and an even grid, sampled, with gaps where the lead came off."] = "固定尺度加等距网格，做了抽稀，脱落的地方留断口。",
        ["Theme"] = "主题",
        ["One chart, in either variant and in a host's own colours."] = "同一张图，明暗两套，以及宿主自己的颜色。",
        ["Palette"] = "色板",
        ["The ten colours, light on the left and dark on the right."] = "那十种颜色：左边浅色、右边深色。",
        ["Interaction"] = "交互",
        ["A tip for every reading, a legend, and a crosshair for the pointer."] = "每个读数一枚提示、一份图例，以及跟随指针的十字线。",
        ["Annotations"] = "标注",
        ["A threshold, a band, and a mark where something happened."] = "一条阈值线、一个区间带，以及事发处的标记。",

        // 每一页自己的那句话。
        ["The acceptance list on one page: a combination chart, two scales, a time axis, stacked columns, a legend, tips, a crosshair, selection - and a hundred thousand readings."] =
            "一页看完验收清单：组合图、双尺度、时间轴、堆叠柱、图例、提示、十字线、选择 —— 还有十万个读数。",
        ["The things on a chart that are not series."] = "图上那些不是系列的东西。",
        ["The five scales a Cartesian chart can be drawn against."] = "笛卡尔图可以对着画的五种尺度。",
        ["Columns lying down, against the names of things."] = "躺下来的柱形，对着事物的名字。",
        ["A price a session: a body from the open to the close, and a wick from the low to the high."] =
            "每个交易日一根价格：实体从开盘到收盘，影线从最低到最高。",
        ["Columns three ways: side by side, stacked, and with a line over them."] = "柱形三种摆法：并排、堆叠、上面压一条线。",
        ["Twenty thousand samples at a fixed scale, sampled down, with dropouts."] = "两万个采样点，固定尺度，做了抽稀，带脱落断口。",
        ["What a reader gets when they point at a chart."] = "读者把指针放到图上时能得到什么。",
        ["One line with its markers, one with an area under it, two step lines, and a line chart with knobs on."] =
            "一条带标记的线、一条带面积的线、两条阶梯线，以及一张带旋钮的折线图。",
        ["A triggered sweep of a live signal, with a timebase, a gain and a trigger level."] = "实时信号的触发扫描：时基、增益与触发电平。",
        ["The ten colours, with the same data drawn light and dark."] = "那十种颜色：同一份数据，浅色与深色各画一遍。",
        ["A whole divided into shares: one wedge per reading, sized by what that reading is worth."] =
            "把整体分成若干份：一个读数一块，大小由那个读数占的份额决定。",
        ["Readings as marks, and the same readings as bubbles."] = "读数画成记号，同一批读数再画成气泡。",
        ["One plot, two vertical scales, and a legend that says which is which."] = "一张图、两条纵轴，图例说明哪条是哪条。",
        ["One chart, in either variant and in a host's own colours."] = "同一张图：明暗两套，以及宿主自己的颜色。",

        // 例子与游乐场的名字。
        ["Columns stacked under a line, with a scale each"] = "线之下堆着柱子，各用一条尺度",
        ["A hundred thousand readings, sampled to the pixels there are"] = "十万个读数，抽稀到屏幕上有多少像素",
        ["Try it: everything at once, with the parts you ask for"] = "试一试：一页看全，要哪些部分自己挑",
        ["A band, a threshold, and a mark for an event"] = "区间带、阈值线，以及一件事的标记",
        ["Try it: the band, the threshold and the mark, where you want them"] = "试一试：把区间带、阈值线和标记放到你要的位置",
        ["Linear, logarithmic, time, duration and category"] = "线性、对数、时刻、时长与分类",
        ["Try it: one scale, fitted or held, with the grid you ask for"] = "试一试：一条尺度，自适应或固定，网格间距随你",
        ["Two years of readings, one bar each"] = "两年的读数，一项一根条",
        ["Try it: the same bars, arranged the way you ask"] = "试一试：同一批条形，按你要的方式排",
        ["Twenty-four sessions of a price"] = "24 个交易日的价格",
        ["Try it: the same sessions, drawn the way you ask"] = "试一试：同一批交易日，按你要的画法",
        ["A week of prices as bars, with a tick at the open and one at the close"] = "一周的价格画成条，开盘与收盘各一根刻线",
        ["Clustered, stacked, and a line over the columns"] = "并排、堆叠，以及柱子上压一条线",
        ["Try it: two years of columns, arranged the way you ask"] = "试一试：两年的柱形，按你要的方式排",
        ["A hundred seconds of a synthetic lead"] = "一百秒的模拟导联",
        ["Try it: the same strip, at the paper speed and the gain you ask for"] = "试一试：同一条走纸，纸速与增益随你",
        ["A tip per reading, a legend, and a crosshair"] = "每个读数一枚提示、一份图例、一条十字线",
        ["Try it: the tips, the crosshair, the legend and the selection"] = "试一试：提示、十字线、图例与选择",
        ["A tip of your own: the series, the reading, a border and a shadow"] = "自定义提示：系列、读数、边框与阴影",
        ["Five ways to draw the same readings"] = "同一批读数的五种画法",
        ["Try it: the same readings, drawn the way you ask"] = "试一试：同一批读数，按你要的画法",
        ["A line with markers, written as markup"] = "一条带标记的线，用标记语言写",
        ["Try it: a scope's controls on a live trace"] = "试一试：示波器的旋钮，作用在实时波形上",
        ["The palette in both variants"] = "两套变体下的色板",
        ["Try it: the palette, one series at a time"] = "试一试：色板，一次看一条系列",
        ["Four shapes: a pie, a pushed-out pie, a doughnut and a rose"] = "四种形状：饼图、炸开的饼图、甜甜圈与玫瑰图",
        ["Labels: inside the wedges, and beside them with a line"] = "标签：写在块里，以及写在旁边用一条线连回来",
        ["Nested: two pies on one circle, each in a ring of its own"] = "嵌套：一个圆上两张饼，各占一环",
        ["Try it: the same shares, drawn the way you ask"] = "试一试：同一批份额，按你要的画法",
        ["A scatter and a bubble per reading"] = "一片散点，每个读数还带一个气泡",
        ["Try it: the marks and the bubbles, drawn the way you ask"] = "试一试：记号与气泡，按你要的画法",
        ["Millimetres on the left, inches on the right"] = "左边毫米，右边英寸",
        ["Try it: the same readings, either scale on either side"] = "试一试：同一批读数，哪条尺度放哪边都行",
        ["Try it: a live feed, at the rate and the window you ask for"] = "试一试：实时数据流，速率与窗口随你",
        ["Try it: the theme under the chart, and the tokens above it"] = "试一试：图下面的主题，以及图上面的令牌",

        // 外壳与设置条。
        ["Dark theme"] = "深色主题",
        ["Language"] = "语言",
        ["Result"] = "结果",
        ["Code"] = "代码",
        ["axaml"] = "axaml",
    };
}
