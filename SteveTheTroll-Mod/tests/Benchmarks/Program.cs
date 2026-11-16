using BenchmarkDotNet.Running;

namespace SteveTheTroll.Benchmarks;

/// <summary>Entry point: `dotnet run -c Release -- --filter "*Name*"` (args are passed on, otherwise BenchmarkDotNet ignores the CLI flags).</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
