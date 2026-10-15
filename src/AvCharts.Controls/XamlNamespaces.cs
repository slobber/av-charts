using Avalonia.Metadata;

// The same namespace the model declares, so that one prefix - `avc` - reaches a chart, its axes, its series and
// its tokens from one markup file. `ChartBase` sits in this namespace and the series it holds in the other, and
// a host should not have to know which.
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Controls")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Controls.Themes")]
