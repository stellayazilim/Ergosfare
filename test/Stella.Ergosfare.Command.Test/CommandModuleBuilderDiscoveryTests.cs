using System.Reflection;
using Stella.Ergosfare.Command.Test.__stubs__;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Reflection-based assembly scanning honors the discovery attributes exactly like
/// source-generated registration: <see cref="ExcludeFromDiscoveryAttribute"/> removes a
/// type from scanning entirely, and <see cref="DiscoveryKeyAttribute"/> gates it behind a
/// key pattern.
/// </summary>
public class CommandModuleBuilderDiscoveryTests
{
    [ExcludeFromDiscovery]
    public sealed record ExcludedStubCommand : ICommand;

    [DiscoveryKey("discovery-tests.keyed")]
    public sealed record KeyedStubCommand : ICommand;

    private sealed class RecordingRegistry : IMessageRegistry
    {
        public List<Type> Registered { get; } = [];

        public int Count => 0;

        public IEnumerator<IMessageDescriptor> GetEnumerator()
        {
            yield break;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Register(Type type) => Registered.Add(type);

        public void RegisterDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
        {
        }
    }

    [Fact]
    public void RegisterFromAssembly_SkipsExcludedAndKeyedTypes()
    {
        var registry = new RecordingRegistry();

        new CommandModuleBuilder(registry).RegisterFromAssembly(Assembly.GetExecutingAssembly());

        Assert.Contains(typeof(StubNonGenericCommand), registry.Registered);
        Assert.DoesNotContain(typeof(ExcludedStubCommand), registry.Registered);
        Assert.DoesNotContain(typeof(KeyedStubCommand), registry.Registered);
    }

    [Fact]
    public void RegisterFromAssembly_WithPattern_SelectsKeyedTypesOnly()
    {
        var registry = new RecordingRegistry();

        new CommandModuleBuilder(registry).RegisterFromAssembly(
            Assembly.GetExecutingAssembly(), "discovery-tests.*");

        Assert.Equal([typeof(KeyedStubCommand)], registry.Registered);
    }

    [Fact]
    public void RegisterFromAssembly_ExcludedTypeIsSkippedEvenByMatchingPattern()
    {
        var registry = new RecordingRegistry();

        new CommandModuleBuilder(registry).RegisterFromAssembly(Assembly.GetExecutingAssembly(), "*");

        Assert.DoesNotContain(typeof(ExcludedStubCommand), registry.Registered);
        Assert.Contains(typeof(KeyedStubCommand), registry.Registered);
        Assert.Contains(typeof(StubNonGenericCommand), registry.Registered);
    }
}
