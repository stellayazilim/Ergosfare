using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Discovery attributes in generated registration: <c>[DiscoveryKey]</c> gates types
///     behind key patterns, <c>[ExcludeFromDiscovery]</c> removes them from discovery
///     entirely. Semantics are asserted by executing the emitted <c>RegisterAll</c>
///     overloads against a real composition catalog and reading back its selection.
/// </summary>
public class DiscoveryKeyTests
{
    /// <summary>
    ///     Emits and loads a generator run's output once, then executes the generated
    ///     <c>RegisterAll</c> overloads against fresh recording registries.
    /// </summary>
    private sealed class DiscoveryHarness
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly Assembly _assembly;
        private readonly Type _registrations;

        public DiscoveryHarness(GeneratorTestHost.GeneratorRunResult result)
        {
            Assert.Empty(result.CompilationErrors);

            // Make sure the real abstractions assembly is loaded so the emitted assembly's
            // references bind to it by name.
            _ = typeof(FrozenCompositionCatalog);

            using var stream = new MemoryStream();
            var emitResult = result.OutputCompilation.Emit(stream);
            Assert.True(emitResult.Success);

            _assembly = Assembly.Load(stream.ToArray());
            _registrations = _assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        }

        /// <summary>
        ///     Runs <c>RegisterAll(compositions)</c> (<paramref name="pattern"/> <c>null</c>)
        ///     or <c>RegisterAll(compositions, pattern)</c> and returns the simple names of
        ///     everything the run selected.
        /// </summary>
        public IReadOnlyList<string> Run(string? pattern = null)
        {
            var catalog = new FrozenCompositionCatalog();

            if (pattern is null)
            {
                _registrations.GetMethod("RegisterAll", [typeof(FrozenCompositionCatalog)])!
                    .Invoke(null, [catalog]);
            }
            else
            {
                _registrations.GetMethod("RegisterAll", [typeof(FrozenCompositionCatalog), typeof(string)])!
                    .Invoke(null, [catalog, pattern]);
            }

            return [.. catalog.Selections.Select(type => type.Name)];
        }
    }

    [Fact]
    public void KeyedTypes_AreGatedOutOfDefaultDiscovery_AndSelectedByKey()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record PlainCommand : ICommand;

                [DiscoveryKey("reporting")]
                public sealed record ReportCommand : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        Assert.Empty(result.GeneratorDiagnostics);
        var harness = new DiscoveryHarness(result);

        Assert.Equal(["PlainCommand"], harness.Run());
        Assert.Equal(["ReportCommand"], harness.Run("reporting"));
        Assert.Equal(["PlainCommand", "ReportCommand"], harness.Run("*"));
        Assert.Empty(harness.Run("other"));
    }

    [Fact]
    public void PrefixGlob_SelectsKeysByPrefix()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [DiscoveryKey("reporting.daily")]
                public sealed record DailyReport : ICommand;

                [DiscoveryKey("reporting.monthly")]
                public sealed record MonthlyReport : ICommand;

                [DiscoveryKey("billing")]
                public sealed record BillingRun : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        var harness = new DiscoveryHarness(result);

        Assert.Equal(["DailyReport", "MonthlyReport"], harness.Run("reporting.*"));
        Assert.Empty(harness.Run());
    }

    [Fact]
    public void EmptyStringKeyAlongsideOthers_KeepsTypeInDefaultDiscovery()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [DiscoveryKey("", "debug")]
                public sealed record DebugCommand : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        var harness = new DiscoveryHarness(result);

        Assert.Equal(["DebugCommand"], harness.Run());
        Assert.Equal(["DebugCommand"], harness.Run("debug"));
    }

    [Fact]
    public void KeyedHandler_DescriptorsRegisterOnlyWhenSelected()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                [DiscoveryKey("reporting")]
                public sealed class ReportingPingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """,
            referenceModuleBuilders: false);

        var harness = new DiscoveryHarness(result);

        // The keyed handler's pre-computed descriptor stays out of default discovery and
        // arrives only when its key is selected.
        Assert.Equal(["Ping"], harness.Run());
        Assert.Equal(["ReportingPingHandler"], harness.Run("reporting"));
    }

    [Fact]
    public void ExcludedType_ProducesNoRegistrationAndNoDiagnostics()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record PlainCommand : ICommand;

                [ExcludeFromDiscovery]
                public sealed record ManuallyWired : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.DoesNotContain("ManuallyWired", result.GeneratedSource);

        var harness = new DiscoveryHarness(result);
        Assert.Equal(["PlainCommand"], harness.Run("*"));
    }

    [Fact]
    public void AssemblyLevelDiscoveryKey_GatesTheLibrarysUntaggedTypes()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record AppCommand : ICommand;
            }
            """,
            libraries:
            [
                ("Ergosfare.TestLibrary.AssemblyKeyed", """
                    using Stella.Ergosfare.Commands.Abstractions;
                    using Stella.Ergosfare.Core.Abstractions.Attributes;

                    [assembly: DiscoveryKey("lib")]

                    namespace TestLib
                    {
                        public sealed record LibCommand : ICommand;

                        [DiscoveryKey("special")]
                        public sealed record SpecialLibCommand : ICommand;
                    }
                    """),
            ]);

        Assert.Empty(result.GeneratorDiagnostics);
        var harness = new DiscoveryHarness(result);

        // The assembly-level key gates the library's untagged types; a type-level key
        // overrides the assembly default.
        Assert.Equal(["AppCommand"], harness.Run());
        Assert.Equal(["LibCommand"], harness.Run("lib"));
        Assert.Equal(["SpecialLibCommand"], harness.Run("special"));
    }

    [Fact]
    public void ExcludedLibraryAssembly_IsNotScannedAtAll()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record AppCommand : ICommand;
            }
            """,
            libraries:
            [
                ("Ergosfare.TestLibrary.Excluded", """
                    using Stella.Ergosfare.Commands.Abstractions;
                    using Stella.Ergosfare.Core.Abstractions.Attributes;

                    [assembly: ExcludeFromDiscovery]

                    namespace TestLib
                    {
                        public sealed record LibCommand : ICommand;

                        internal sealed record HiddenLibCommand : ICommand;
                    }
                    """),
            ]);

        // No registrations from the excluded assembly — and no ERGOSG002 for its
        // internals either: the exclusion is deliberate.
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.DoesNotContain("LibCommand", result.GeneratedSource);

        var harness = new DiscoveryHarness(result);
        Assert.Equal(["AppCommand"], harness.Run("*"));
    }
}
