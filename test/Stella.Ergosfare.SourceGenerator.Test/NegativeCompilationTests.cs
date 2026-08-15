using System.Diagnostics;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// The dead-dispatch judgment as an application actually meets it: a real project, built by
/// a real MSBuild, failing. The in-process generator tests pin the rule; this pins the
/// chain around it — the analyzer travelling as an analyzer, the package's props, and K5's
/// composition-root gate keying off a genuine executable <c>OutputKind</c>.
/// </summary>
/// <remarks>
/// The fixture is deliberately absent from <c>Stella.Ergosfare.slnx</c>: it is meant not to
/// compile, so a solution build must never pick it up. It is built here on demand instead.
/// </remarks>
public class NegativeCompilationTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public void DispatchingAMessageNoHandlerCanServe_FailsTheBuild()
    {
        var fixture = Path.Combine(RepositoryRoot(), "test", "fixtures", "NoHandlerDispatch", "NoHandlerDispatch.csproj");
        Assert.True(File.Exists(fixture), $"negative-compilation fixture missing at '{fixture}'");

        var (exitCode, output) = Build(fixture);

        Assert.False(exitCode == 0, $"the fixture was expected not to compile, but the build succeeded:{Environment.NewLine}{output}");
        Assert.Contains("ERGO005", output, StringComparison.Ordinal);
        Assert.Contains("Orphan", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Serializes the fixture build across processes. The suite multi-targets, so an
    /// unfiltered <c>dotnet test</c> runs one host per TFM and both reach this test at
    /// once — two MSBuilds against one project tree, which wedges on the shared
    /// <c>obj/</c> lock rather than failing. Per-TFM output paths are not the way out:
    /// overriding <c>BaseOutputPath</c>/<c>BaseIntermediateOutputPath</c> breaks restore,
    /// which is why the fixture keeps its own tree.
    /// </summary>
    private static readonly string FixtureBuildMutexName =
        "Stella.Ergosfare.NegativeCompilationFixture." + Environment.UserName;

    private static (int ExitCode, string Output) Build(string project)
    {
        using var gate = new Mutex(initiallyOwned: false, FixtureBuildMutexName);
        var held = false;

        try
        {
            try
            {
                held = gate.WaitOne(TimeSpan.FromMinutes(5));
            }
            catch (AbandonedMutexException)
            {
                // The holder died mid-build; the lock is ours and the tree is whatever it
                // left behind, which the build below rebuilds anyway.
                held = true;
            }

            Assert.True(held, "another process held the fixture build lock for over five minutes");

            var startInfo = new ProcessStartInfo("dotnet")
            {
                // The fixture has its own output tree and is in no solution, so an on-demand
                // build shares nothing with the solution build running these tests.
                //
                // -nodeReuse:false and -m:1 are not tuning: a persistent MSBuild worker
                // node inherits this process's redirected pipes and outlives the build,
                // so ReadToEnd() waits on a writer that never closes and the test host
                // hangs instead of failing. The env vars close the same door on the
                // MSBuild server the CLI would otherwise reuse.
                ArgumentList = { "build", project, "--nologo", "-v", "q", "-nodeReuse:false", "-m:1" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                Environment =
                {
                    ["MSBUILDDISABLENODEREUSE"] = "1",
                    ["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1",
                },
            };

            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("could not start 'dotnet build'");

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output);
        }
        finally
        {
            if (held)
            {
                gate.ReleaseMutex();
            }
        }
    }

    /// <summary>Walks up from the test binaries to the directory holding the solution.</summary>
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("Stella.Ergosfare.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
