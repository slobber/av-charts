using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// The rule from the design brief: the model assembly is allowed Avalonia's base half and nothing else.
/// Avalonia 12 ships Base, Controls and Markup.Xaml in one package, so the package graph cannot express this -
/// a reference check can.
/// </summary>
public class AssemblyBoundaryTests
{
    [Fact]
    public void The_model_assembly_does_not_reference_avalonia_controls_or_markup()
    {
        var referenced = typeof(ChartValue).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("Avalonia.Controls", referenced);
        Assert.DoesNotContain("Avalonia.Markup.Xaml", referenced);
        Assert.DoesNotContain("Avalonia.Themes.Simple", referenced);
    }
}
