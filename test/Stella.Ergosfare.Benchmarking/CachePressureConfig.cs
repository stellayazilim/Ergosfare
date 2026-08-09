using System.Security.Principal;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace Stella.Ergosfare.Benchmarking;

/// <summary>
/// The measurement gate for <see cref="CachePressureBenchmark"/>: one logical core, plus
/// the hardware counters that say what a dispatch costs beyond its wall clock.
/// </summary>
/// <remarks>
/// <para><b>Single-core affinity.</b> Every case runs pinned to logical core 0. The
/// deployment this library is tuned for is a single-core server, where a dispatch shares
/// one L1/L2 with everything else the process does — a free-floating benchmark spreads
/// its working set over several caches and reports a hit rate that machine never sees.
/// Pinning also stops the scheduler from migrating the run mid-iteration, which is what
/// makes cache-miss counts comparable between rows at all.</para>
/// <para><b>Hardware counters require an elevated console on Windows.</b> BenchmarkDotNet
/// reads them through ETW kernel sessions, which a non-elevated process may not open. The
/// counters are therefore attached only when this process can actually collect them;
/// without elevation the same run still produces the timing and allocation columns, and
/// the counter columns are simply absent. Run it from an administrator console —
/// <c>dotnet run -c Release -f net9.0 --project test\Stella.Ergosfare.Benchmarking -- --filter *CachePressure*</c>
/// — to fill them in. On Linux the equivalent is <c>perf</c> and root/perf_event_paranoid
/// permissions; the gate below reports no counters there.</para>
/// </remarks>
public sealed class CachePressureConfig : ManualConfig
{
    /// <summary>Creates the configuration.</summary>
    public CachePressureConfig()
    {
        // Affinity is a bitmask over logical processors; bit 0 is core 0.
        AddJob(Job.Default
            .WithAffinity((nint)1)
            .WithId("SingleCore"));

        if (CanCollectHardwareCounters())
        {
            AddHardwareCounters(
                HardwareCounter.CacheMisses,
                HardwareCounter.BranchMispredictions,
                HardwareCounter.InstructionRetired);
        }
    }

    /// <summary>
    /// Whether this process can open the counter session — Windows, elevated. Asking
    /// first keeps an unelevated run useful (timings and allocations) instead of failing
    /// validation with nothing to show.
    /// </summary>
    private static bool CanCollectHardwareCounters()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
