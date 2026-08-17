using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Text;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// The hand-written equality of the generator's remaining incremental models, swept the same
/// way <see cref="RegistrableTypeModelEqualityTests"/> sweeps its own: every property is
/// cleared in turn and the model must notice.
/// </summary>
/// <remarks>
/// <para>
/// These models are the incremental pipeline's cache keys. A property missing from
/// <c>Equals</c> is the quietest possible defect — the generator serves the previous output,
/// no diagnostic is raised, and the build succeeds with a stale file in it.
/// </para>
/// <para>
/// Some properties are deliberately <b>not</b> part of equality: a display name and a source
/// location change nothing about what gets emitted, and including them would invalidate the
/// cache on a whitespace edit. Those are named per model below, so the exclusion is a decision
/// on record rather than an omission that looks like one.
/// </para>
/// </remarks>
public class GeneratorModelEqualityTests
{
    private static readonly LocationInfo Where = new("Probe.cs", new TextSpan(1, 2), new LinePositionSpan());

    private static readonly LocationInfo Elsewhere = new("Other.cs", new TextSpan(3, 4), new LinePositionSpan());

    /// <summary>
    /// Clears one property at a time and checks the verdict against what the model promises:
    /// a change must be noticed unless the property is named in <paramref name="excluded"/>,
    /// in which case it must be ignored. Properties without a setter are computed from the
    /// others and have nothing of their own to compare.
    /// </summary>
    private static void Sweep<TModel>(Func<TModel> populated, params string[] excluded)
        where TModel : IEquatable<TModel>
    {
        var baseline = populated();

        Assert.True(baseline.Equals(populated()), "two identically populated models must compare equal");
        Assert.Equal(baseline.GetHashCode(), populated()!.GetHashCode());

        var swept = 0;

        foreach (var property in typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            swept++;
            object boxed = populated()!;
            property.SetValue(boxed, Cleared(property.PropertyType));

            var stillEqual = baseline.Equals((TModel)boxed);

            if (excluded.Contains(property.Name))
            {
                Assert.True(stillEqual,
                    $"'{property.Name}' is documented as outside equality, but changing it changed the verdict — "
                    + "either the model or this list is wrong.");
            }
            else
            {
                Assert.False(stillEqual,
                    $"'{property.Name}' changed and the model still compared equal — it is missing from Equals, "
                    + "so an edit that only touches it would be served from the incremental cache.");
            }
        }

        Assert.True(swept > 0, $"{typeof(TModel).Name} exposed no settable property to sweep");
    }

    /// <summary>
    /// The empty value of a property's type: <c>null</c> for anything nullable, an empty array
    /// for the collections (a <c>default</c> <see cref="ImmutableArray{T}"/> throws when
    /// equality reads its length), and the type's own default for everything else — which is
    /// why every populated model below names a non-default enum member and sets its flags.
    /// </summary>
    private static object? Cleared(Type type)
    {
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
        {
            return null;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            return typeof(ImmutableArray)
                .GetMethod(nameof(ImmutableArray.Create), BindingFlags.Public | BindingFlags.Static, [])!
                .MakeGenericMethod(type.GetGenericArguments())
                .Invoke(null, null);
        }

        return Activator.CreateInstance(type);
    }

    private static DispatchSiteModel Site() => new()
    {
        MessageTypeExpression = "global::TestApp.Ping",
        MessageTypeMetadataName = "TestApp.Ping",
        DisplayName = "TestApp.Ping",
        Kind = DispatchSiteKind.Message,
        IsOpaque = true,
        IsValueType = true,
        IsStreamMessage = true,
        IsGenericMessage = true,
        AssignableKeys = ["global::TestApp.IPing"],
        Groups = ["reporting"],
        HasUnprovableGroups = true,
        Location = Where,
        ReferencedAssemblyName = "TestLib",
    };

    private static RegistrationSiteModel Registration() => new()
    {
        TypeMetadataName = "TestApp.PingHandler",
        MainHandlerMessageKeys = ["global::TestApp.Ping"],
        IsOpaque = true,
        UnknownTypeLocation = Where,
    };

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void DispatchSite_ComparesEveryProperty()
    {
        // Every property here is evidence the reachability judgment reads, including the
        // location and the owning assembly — a site rehydrated from a referenced manifest is
        // not the same site as one written in this compilation.
        Sweep(Site);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void DispatchSite_ComparesAssignableKeysAndGroupsElementwise()
    {
        // Same lengths, different contents: the length check the equality opens with cannot
        // catch this, and the element loops after it are the only thing that can.
        Assert.NotEqual(Site(), Site() with { AssignableKeys = ["global::TestApp.IPong"] });
        Assert.NotEqual(Site(), Site() with { Groups = ["auditing"] });

        Assert.Equal(Site(), Site() with { AssignableKeys = ["global::TestApp.IPing"] });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void DispatchSite_EqualsAcrossTheBoxedOverloadAndNothingElse()
    {
        Assert.True(Site().Equals((object) Site()));
        Assert.False(Site().Equals("not a model"));
        Assert.False(Site().Equals(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RegistrationSite_ComparesEveryProperty()
    {
        Sweep(Registration);

        // Same length, different key: the element loop again.
        Assert.NotEqual(Registration(), Registration() with { MainHandlerMessageKeys = ["global::TestApp.Pong"] });

        Assert.True(Registration().Equals((object) Registration()));
        Assert.False(Registration().Equals("not a model"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ManifestScan_ComparesBothFlagsAndBothSiteLists()
    {
        var populated = new DispatchManifestScanResult([Site()], [Registration()],
            hasUnknownSiteAssemblies: true, hasOpaqueRegistrations: true);

        Assert.Equal(populated,
            new DispatchManifestScanResult([Site()], [Registration()], true, true));
        Assert.Equal(populated.GetHashCode(),
            new DispatchManifestScanResult([Site()], [Registration()], true, true).GetHashCode());

        // The two flags are what suspend whole diagnostics closure-wide, so a scan that
        // differs only in one of them is not the same scan.
        Assert.NotEqual(populated,
            new DispatchManifestScanResult([Site()], [Registration()], false, true));
        Assert.NotEqual(populated,
            new DispatchManifestScanResult([Site()], [Registration()], true, false));

        // Both lists compare by length and then element-wise, so a same-length list with a
        // different member has to be caught by the loop.
        Assert.NotEqual(populated,
            new DispatchManifestScanResult([Site() with { DisplayName = "TestApp.Pong" }], [Registration()], true, true));
        Assert.NotEqual(populated,
            new DispatchManifestScanResult([Site()], [Registration() with { IsOpaque = false }], true, true));
        Assert.NotEqual(populated, new DispatchManifestScanResult([], [Registration()], true, true));
        Assert.NotEqual(populated, new DispatchManifestScanResult([Site()], [], true, true));

        Assert.True(populated.Equals((object) new DispatchManifestScanResult([Site()], [Registration()], true, true)));
        Assert.False(populated.Equals("not a scan"));

        // The empty scan is the "nothing referenced anything" baseline every judgment starts
        // from, and it must not accidentally equal a populated one.
        Assert.NotEqual(populated, DispatchManifestScanResult.Empty);
        Assert.Empty(DispatchManifestScanResult.Empty.Sites);
        Assert.Empty(DispatchManifestScanResult.Empty.RegistrationSites);
        Assert.False(DispatchManifestScanResult.Empty.HasUnknownSiteAssemblies);
        Assert.False(DispatchManifestScanResult.Empty.HasOpaqueRegistrations);
    }

    private static PluginConstraintModel Constraints() => new(
        ["global::TestApp.ICacheable"], RequiresReferenceType: true, RequiresValueType: true, IsUnmodelable: true);

    private static PluginInvocationModel Invocation() => new(
        "global::TestApp.Tracing",
        "TestApp.Tracing",
        "Began",
        PluginHook.Finish,
        IsAsync: true,
        IsStatic: true,
        PluginModule.All,
        ["tenanted"],
        [new PluginParameterBinding(default, "global::TestApp.Clock")],
        Constraints(),
        Where);

    private static PluginServiceModel Service() => new(
        "global::TestApp.Tracing",
        "TestApp.Tracing",
        ["global::TestApp.Clock"],
        CannotReceiveOptions: true,
        Where,
        "TestApp",
        "Tracing",
        "record");

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PluginConstraints_CompareEveryProperty()
    {
        Sweep(Constraints);

        // Same length, different bound: the shape filter would otherwise admit a plan whose
        // message does not satisfy the constraint, and the emitted call would not compile.
        Assert.NotEqual(Constraints(), Constraints() with { MessageTypes = ["global::TestApp.IOther"] });

        // A default array and an empty one are the same absence — discovery produces both.
        Assert.Equal(PluginConstraintModel.None,
            PluginConstraintModel.None with { MessageTypes = default });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PluginInvocation_ComparesWhatDecidesEmission_AndIgnoresWhatDoesNot()
    {
        // A display name and a source location are for diagnostics; the call written into a
        // plan is identical either way, so they stay out of the cache key.
        Sweep(Invocation, nameof(PluginInvocationModel.ServiceDisplayName), nameof(PluginInvocationModel.Location));

        Assert.NotEqual(Invocation(), Invocation() with { Keys = ["other"] });
        Assert.NotEqual(Invocation(),
            Invocation() with { Parameters = [new PluginParameterBinding(default, "global::TestApp.Other")] });
        Assert.NotEqual(Invocation(), Invocation() with { Constraints = PluginConstraintModel.None });

        Assert.Equal(Invocation().GetHashCode(), (Invocation() with { Location = Elsewhere }).GetHashCode());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PluginInvocation_SelectsTheDefaultKeyOnlyWhenItNamesNoKey()
    {
        // Saying nothing about keys selects the default key alone — a keyed construct was
        // opted out of default discovery by its author, and silence must not opt it back in.
        Assert.False(Invocation().SelectsDefaultKeyOnly);
        Assert.True((Invocation() with { Keys = [] }).SelectsDefaultKeyOnly);
        Assert.True((Invocation() with { Keys = default }).SelectsDefaultKeyOnly);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PluginService_ComparesWhatTheModuleWrites_AndIgnoresWhatItDoesNot()
    {
        Sweep(Service, nameof(PluginServiceModel.DisplayName), nameof(PluginServiceModel.Location));

        // The constructor arguments decide the emitted activation, one entry per parameter,
        // so a different binding in the same slot is a different service.
        Assert.NotEqual(Service(), Service() with { ConstructorArguments = ["global::TestApp.Other"] });

        // A null entry is the options instance rather than a resolved type — the two are not
        // interchangeable even though both are "one argument".
        Assert.NotEqual(Service(), Service() with { ConstructorArguments = [null] });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PluginFacade_ComparesTheNameTheOptionsTypeAndTheServices()
    {
        var facade = new PluginFacadeModel("Tracing", "global::TestApp.TracingOptions", "TestApp.TracingOptions",
            [Service()]);

        Sweep(() => new PluginFacadeModel("Tracing", "global::TestApp.TracingOptions", "TestApp.TracingOptions",
                [Service()]),
            nameof(PluginFacadeModel.OptionsDisplayName));

        Assert.NotEqual(facade, facade with { Services = [Service() with { TypeExpression = "global::TestApp.Other" }] });
        Assert.Equal(facade.GetHashCode(),
            (facade with { OptionsDisplayName = "anything" }).GetHashCode());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void SequenceEquality_TreatsADefaultArrayAsAnEmptyOne()
    {
        ImmutableArray<string> missing = default;
        var empty = ImmutableArray<string>.Empty;
        ImmutableArray<string> one = ["a"];
        ImmutableArray<string> other = ["b"];
        ImmutableArray<string> two = ["a", "b"];

        // Discovery leaves both absences behind, and the incremental cache would otherwise
        // rebuild every time the shape flipped between them.
        Assert.True(missing.SequenceEqualOrBothEmpty(empty));
        Assert.True(empty.SequenceEqualOrBothEmpty(missing));
        Assert.True(missing.SequenceEqualOrBothEmpty(missing));

        Assert.False(one.SequenceEqualOrBothEmpty(missing));
        Assert.False(missing.SequenceEqualOrBothEmpty(one));
        Assert.False(one.SequenceEqualOrBothEmpty(two));
        Assert.False(one.SequenceEqualOrBothEmpty(other));

        Assert.True(one.SequenceEqualOrBothEmpty(["a"]));
        Assert.True(two.SequenceEqualOrBothEmpty(["a", "b"]));
    }
}
