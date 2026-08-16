#pragma warning disable ERGOEXP002 // the surface under test is the experimental one

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Plugins.Abstractions;

// System.Reflection is needed here for the metadata assertions and brings its own Module.
using Module = Stella.Ergosfare.Plugins.Abstractions.Module;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// The plugin surface is declarative: a plugin author writes attributes and the generator
/// reads them off symbols, so the emission tests never construct one. What is left unproven
/// by those tests is everything the declarations themselves promise — what an unfiltered
/// filter selects, where each attribute may be written, and that the experimental id the
/// surface is stamped with is the one consumers are told to suppress.
/// </summary>
/// <remarks>
/// These are the promises the documentation makes to a plugin author, checked against the
/// metadata a consumer actually sees. A change here is a change to the plugin contract, not
/// to an implementation detail — which is why they are worth writing down twice.
/// </remarks>
public class PluginSurfaceContractTests
{
    private sealed class TracingOptions;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void FamilyFilter_SelectsTheNamedFamilies_AndSaysNothingAboutKeys()
    {
        var filter = new PluginServiceFilterAttribute(Module.Command | Module.Event);

        Assert.Equal(Module.Command | Module.Event, filter.Modules);

        // Saying nothing about keys is not "every key": an empty key list selects the
        // default key alone, exactly as a pattern-less RegisterGenerated() does.
        Assert.Empty(filter.Keys);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void KeyFilter_NarrowsKeysWhileLeavingEveryFamilyIn()
    {
        var filter = new PluginServiceFilterAttribute("tenanted", string.Empty);

        // The two axes are independent: filtering by key must not silently also filter by
        // family, or a keyed plugin would stop reaching queries and events.
        Assert.Equal(Module.All, filter.Modules);
        Assert.Equal(new[] { "tenanted", string.Empty }, filter.Keys);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void KeyFilter_WithNoKeysAtAll_IsTheUnfilteredFilter()
    {
        var filter = new PluginServiceFilterAttribute();

        Assert.Equal(Module.All, filter.Modules);
        Assert.Empty(filter.Keys);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Families_AreFlags_AndAllIsExactlyTheThreeOfThem()
    {
        // All is a composite rather than a sentinel, so a filter can be tested with HasFlag
        // and a new family would have to be added to it deliberately.
        Assert.Equal(Module.Command | Module.Query | Module.Event, Module.All);
        Assert.Equal((Module) 0, Module.None);
        Assert.True(Module.All.HasFlag(Module.Query));
        Assert.False(Module.None.HasFlag(Module.Query));
        Assert.NotNull(typeof(Module).GetCustomAttribute<FlagsAttribute>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PluginDeclaration_CarriesTheNameAndTheOptionalSettingsType()
    {
        var withSettings = new ErgosfarePluginAttribute("Tracing", typeof(TracingOptions));

        Assert.Equal("Tracing", withSettings.Name);
        Assert.Equal(typeof(TracingOptions), withSettings.OptionsType);

        // No settings type is the parameterless Add<Name> case, and it must be expressible
        // by omission rather than by passing something.
        var withoutSettings = new ErgosfarePluginAttribute("Tracing");

        Assert.Equal("Tracing", withoutSettings.Name);
        Assert.Null(withoutSettings.OptionsType);
    }

    [Theory]
    [InlineData(Hook.Start)]
    [InlineData(Hook.PreMain)]
    [InlineData(Hook.PostMain)]
    [InlineData(Hook.Finish)]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void InvokableDeclaration_CarriesTheHookItWasWrittenWith(Hook hook)
    {
        Assert.Equal(hook, new PipelineInvokableAttribute(hook).Hook);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void FilterAndInvokable_MayBeWrittenMoreThanOnce_AndAreNotInherited()
    {
        var filter = typeof(PluginServiceFilterAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Filters stack — several on one target intersect — which takes AllowMultiple.
        Assert.True(filter.AllowMultiple);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Method, filter.ValidOn);

        // Not inherited: emission is decided from what the type itself declares, so a
        // derived service must not silently pick up its base's filter.
        Assert.False(filter.Inherited);

        var invokable = typeof(PipelineInvokableAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // One method can serve several hooks.
        Assert.True(invokable.AllowMultiple);
        Assert.Equal(AttributeTargets.Method, invokable.ValidOn);
        Assert.False(invokable.Inherited);

        var declaration = typeof(ErgosfarePluginAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // An assembly is one plugin, so this one is single-use by construction.
        Assert.Equal(AttributeTargets.Assembly, declaration.ValidOn);
        Assert.False(declaration.AllowMultiple);
    }

    [Theory]
    [InlineData(typeof(ErgosfarePluginAttribute))]
    [InlineData(typeof(PipelineInvokableAttribute))]
    [InlineData(typeof(PluginServiceFilterAttribute))]
    [InlineData(typeof(Hook))]
    [InlineData(typeof(Module))]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void EverySurfaceType_IsStampedWithThePublishedExperimentalId(Type surface)
    {
        var experimental = surface.GetCustomAttribute<ExperimentalAttribute>();

        Assert.NotNull(experimental);

        // Plugins.Abstractions spells the id itself rather than referencing the core's
        // declaration — deliberately, so the surface references nothing. The cost of that
        // choice is exactly this drift, which is why it is asserted rather than assumed.
        Assert.Equal(ExperimentalIds.PluginSurface, experimental!.DiagnosticId);
    }
}
