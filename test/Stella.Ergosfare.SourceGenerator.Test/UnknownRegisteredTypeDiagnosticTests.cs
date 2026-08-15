using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     ERGO018: a <c>Register</c> call that names its type at run time. The world is
///     closed — a construct's pipeline is compiled, its composition frozen and its plan
///     baked from what the compilation can see — so a type only run time knows enters none
///     of it, and the registration cannot mean what it appears to mean.
/// </summary>
public class UnknownRegisteredTypeDiagnosticTests
{
    private const string Preamble = """
        using System;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
        using Stella.Ergosfare.Core.Abstractions;

        namespace TestApp
        {
            public sealed record Ping : ICommand;

            public sealed class PingHandler : ICommandHandler<Ping>
            {
                public ValueTask HandleAsync(Ping message, ErgosfareContext context) => default;
            }

        """;

    private static void AssertSingle018(GeneratorTestHost.GeneratorRunResult result)
    {
        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGO018");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    /// <summary>
    ///     The parameter's declared type is <c>System.Type</c>; its <em>value</em> is what
    ///     names the registered type, and a value is a run-time thing.
    /// </summary>
    [Fact]
    public void RegisterOfARuntimeTypeValue_FailsTheBuild()
    {
        AssertSingle018(GeneratorTestHost.Run(Preamble + """
                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands, Type runtimeType)
                        => commands.Register(runtimeType);
                }
            }
            """));
    }

    /// <summary>
    ///     A name resolved from a string is the same shape one step further away — and the
    ///     shape the trimmer and AOT cannot follow either.
    /// </summary>
    [Fact]
    public void RegisterOfATypeResolvedFromAString_FailsTheBuild()
    {
        AssertSingle018(GeneratorTestHost.Run(Preamble + """
                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands, string configuredName)
                        => commands.Register(Type.GetType(configuredName));
                }
            }
            """));
    }

    /// <summary>
    ///     <c>Register&lt;T&gt;()</c> inside a generic method names a different type per
    ///     instantiation, none of which this compilation can enumerate.
    /// </summary>
    [Fact]
    public void RegisterOfAnOpenTypeParameter_FailsTheBuild()
    {
        AssertSingle018(GeneratorTestHost.Run(Preamble + """
                public static class Boot
                {
                    public static void Add<T>(CommandModuleBuilder commands) where T : class, ICommand
                        => commands.Register<T>();
                }
            }
            """));
    }

    /// <summary>
    ///     The two provable spellings. Nothing is reported for either — this is the whole
    ///     supported surface, and the diagnostic must not stand in its way.
    /// </summary>
    [Fact]
    public void TypeofAndConcreteGenericRegistrations_AreClean()
    {
        var result = GeneratorTestHost.Run(Preamble + """
                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands)
                    {
                        commands.Register(typeof(PingHandler));
                        commands.Register<Ping>();
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO018");
    }

    /// <summary>
    ///     <c>RegisterParticipants</c> takes an <c>IEnumerable&lt;Type&gt;</c> by design —
    ///     it is the bulk channel <c>RegisterGenerated()</c> emits, and the types it carries
    ///     were known when the generator wrote the call. Opaque, but not a defect.
    /// </summary>
    [Fact]
    public void TheGeneratedBulkChannel_IsNotReported()
    {
        var result = GeneratorTestHost.Run(Preamble + """
                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands, System.Collections.Generic.List<Type> batch)
                        => commands.RegisterParticipants(batch);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO018");
    }

    /// <summary>
    ///     A <c>typeof</c> parked in a local first. The check is syntactic — the argument
    ///     expression has to <em>be</em> the <c>typeof</c> — so this is reported even though
    ///     a reader can see the type, and the fix is to inline the <c>typeof</c>.
    /// </summary>
    /// <remarks>
    ///     Pinned deliberately, not because it is desirable: the alternative is to follow
    ///     single-assignment locals, which is dataflow the check does not do today. The test
    ///     exists so that closing the gap fails here and reads as the improvement it is,
    ///     rather than as a regression.
    /// </remarks>
    [Fact]
    public void RegisterOfATypeofParkedInALocal_IsReportedToo()
    {
        AssertSingle018(GeneratorTestHost.Run(Preamble + """
                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands)
                    {
                        Type t = typeof(PingHandler);
                        commands.Register(t);
                    }
                }
            }
            """));
    }
}
