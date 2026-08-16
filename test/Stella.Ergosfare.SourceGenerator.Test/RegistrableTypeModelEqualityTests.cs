using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Text;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// The incremental pipeline skips a stage when its input compares equal to last time, so
/// <see cref="RegistrableTypeModel"/>'s hand-written equality is what decides whether an edit
/// is seen at all. A property left out of it is invisible in the worst way: the generator
/// serves the previous output and nothing reports anything — no diagnostic, no wrong build,
/// just a stale file.
/// </summary>
/// <remarks>
/// Written as a mutation sweep rather than a list of cases so a property added later is
/// covered without anyone remembering to cover it. The populated baseline is the second half
/// of that: every property is <c>required</c>, so adding one breaks this file at compile time
/// and the sweep picks it up as soon as it is given a value.
/// </remarks>
public class RegistrableTypeModelEqualityTests
{
    /// <summary>
    /// The baseline, with a nested model in <c>DerivedEventMessages</c> so that field is
    /// populated like every other. Built from <see cref="Leaf"/> rather than from itself:
    /// the model nests its own type, so a self-call would not terminate.
    /// </summary>
    private static RegistrableTypeModel Populated()
        => Leaf() with { DerivedEventMessages = [Leaf()] };

    private static RegistrableTypeModel Leaf() => new()
    {
        TypeofExpression = "global::TestApp.Ping",
        DisplayName = "TestApp.Ping",
        IsCommand = true,
        IsQuery = true,
        IsEvent = true,
        IsAccessible = true,
        Location = new LocationInfo("Ping.cs", new TextSpan(1, 2), new LinePositionSpan()),
        Weight = 7,
        GroupsExpression = "new[] { \"a\" }",
        GroupNames = ["a"],
        Descriptors = [new DescriptorModel(DescriptorKind.MainHandler, "global::TestApp.Ping", "global::System.Threading.Tasks.ValueTask")],
        ReferencedAssemblyName = "TestLib",
        DiscoveryKeys = ["k"],
        IsDispatchableMessage = true,
        IsMessageShape = true,
        DispatchResults = [new DispatchResultModel("string", false, false)],
        IsDirectlyConstructible = true,
        ProviderConstructionExpression = "new global::TestApp.Ping()",
        ProviderConstructionUsesKeyedServices = true,
        HasPipelineExclusion = true,
        ExcludedInterceptorGroups = ["g"],
        IsValueType = true,
        IsNestedType = true,
        IsGenericParticipant = true,
        MonomorphizedFrom = "global::TestApp.Ping<>",
        AssignableKeys = ["global::TestApp.Ping"],
        ContractShapes = [new ContractShapeModel(DescriptorKind.MainHandler, true, true, "global::TestApp.Ping", "string")],
        StagedConstructionExpression = "new global::TestApp.Ping()",
        StagedConstructionUsesKeyedServices = true,
        HasMultiplePublicConstructors = true,
        HasFromServicesConstructorParameter = true,
        InfoLocation = new LocationInfo("Ping.cs", new TextSpan(3, 4), new LinePositionSpan()),
        IsExcludedFromDiscovery = true,
        MetadataSortKey = "TestApp.Ping",
        ResultAdapter = new ResultAdapterModel("global::TestApp.Adapter", "TestApp.Adapter", "string", "string", true, true, true),
        HasIgnoredResultAdapter = true,
        ImplementsMessageMarker = true,
        DerivedEventMessages = ImmutableArray<RegistrableTypeModel>.Empty,
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryProperty_ParticipatesInEquality()
    {
        var baseline = Populated();

        Assert.True(baseline.Equals(Populated()), "two identically populated models must compare equal");

        foreach (var property in typeof(RegistrableTypeModel)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object boxed = Populated();
            property.SetValue(boxed, Cleared(property.PropertyType));

            Assert.False(
                baseline.Equals((RegistrableTypeModel)boxed),
                $"'{property.Name}' changed and the model still compared equal — it is missing from Equals, "
                + "so an edit that only touches it would be served from the incremental cache.");
        }
    }

    /// <summary>
    /// The empty value of a property's type: <c>null</c> for anything nullable, an empty
    /// array for the collections (a <c>default</c> <see cref="ImmutableArray{T}"/> throws when
    /// equality reads its length), and the type's own default for everything else.
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
}
