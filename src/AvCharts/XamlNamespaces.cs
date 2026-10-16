using Avalonia.Metadata;

// The namespaces a markup file uses to reach the model: axes, series, values, the drawing kernel, the transforms
// and the elements. The controls assembly declares the same one, so a page needs a single prefix for the whole
// library - and `avc` is the prefix the namespace asks a designer for.
//
// One attribute per namespace rather than one for the family, because a declaration maps a namespace exactly:
// the model's types are spread over six of them, one per folder they live in.
//
// The model assembly is allowed Avalonia's base half and nothing else, and this attribute is in that half: a
// namespace declaration is metadata rather than a dependency on the markup layer.
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Axes")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Series")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Data")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Drawing")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Transforms")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Elements")]
[assembly: XmlnsPrefix("https://github.com/slobber/av-charts", "avc")]
