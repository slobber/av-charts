using BenchmarkDotNet.Running;

namespace AvCharts.Benchmarks;

/// <summary>
/// Measures what the design brief's performance table asks for, on the machine it is run on.
/// </summary>
/// <remarks>
/// <para>
/// Run it in Release, from the repository root:
/// </para>
/// <code>
/// dotnet run -c Release --project src/AvCharts.Benchmarks
/// </code>
/// <para>
/// The numbers it prints are what the §11 table in the design brief is filled in with. They are not a gate:
/// the frame rate is measured on the machine a chart is meant to run on, by hand, and continuous integration
/// only holds a coarse disaster budget (see the tests). A benchmark on a shared build agent says more about the
/// agent than about the library.
/// </para>
/// </remarks>
internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
