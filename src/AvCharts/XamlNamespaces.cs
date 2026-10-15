using Avalonia.Metadata;

// The namespace a markup file uses to reach the model: axes, transforms, series and values. The controls
// assembly declares the same one, so a page needs a single prefix for the whole library - and `avc` is the
// prefix the namespace asks a designer for.
//
// The model assembly is allowed Avalonia's base half and nothing else, and this attribute is in that half: a
// namespace declaration is metadata rather than a dependency on the markup layer.
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts")]
[assembly: XmlnsDefinition("https://github.com/slobber/av-charts", "AvCharts.Charts")]
[assembly: XmlnsPrefix("https://github.com/slobber/av-charts", "avc")]
