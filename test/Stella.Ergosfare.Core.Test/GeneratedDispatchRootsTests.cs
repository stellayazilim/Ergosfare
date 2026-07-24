using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The generated-dispatch-roots store: idempotent registration, lookup, and generic
/// re-entry through the visitors — including value-type messages, which are the cases
/// shared generic code cannot serve under Native AOT.
/// </summary>
public class GeneratedDispatchRootsTests
{
    private sealed record ClassMessage : IMessage;

    private readonly record struct StructMessage : IMessage;

    private sealed class TypeProbeVisitor : IMessageRootVisitor<Type, bool>
    {
        public static readonly TypeProbeVisitor Instance = new();

        public Type Visit<TMessage>(bool state) where TMessage : IMessage => typeof(TMessage);
    }

    private sealed class PairProbeVisitor : IMessageResultRootVisitor<(Type, Type), bool>
    {
        public static readonly PairProbeVisitor Instance = new();

        public (Type, Type) Visit<TMessage, TResult>(bool state) where TMessage : IMessage
            => (typeof(TMessage), typeof(TResult));
    }

    [Fact]
    public void AddMessage_RootsClassAndStructMessages_AndVisitorReentersGenerically()
    {
        GeneratedDispatchRoots.AddMessage<ClassMessage>();
        GeneratedDispatchRoots.AddMessage<StructMessage>();

        var classRoot = GeneratedDispatchRoots.FindMessage(typeof(ClassMessage));
        var structRoot = GeneratedDispatchRoots.FindMessage(typeof(StructMessage));

        Assert.NotNull(classRoot);
        Assert.NotNull(structRoot);
        Assert.Equal(typeof(ClassMessage), classRoot!.Accept(TypeProbeVisitor.Instance, state: false));
        Assert.Equal(typeof(StructMessage), structRoot!.Accept(TypeProbeVisitor.Instance, state: false));
    }

    [Fact]
    public void AddResultAndStream_AreKeyedByMessageAndResultPair()
    {
        GeneratedDispatchRoots.AddResult<ClassMessage, string>();
        GeneratedDispatchRoots.AddStream<ClassMessage, int>();

        var resultRoot = GeneratedDispatchRoots.FindResult(typeof(ClassMessage), typeof(string));

        Assert.NotNull(resultRoot);
        Assert.Equal((typeof(ClassMessage), typeof(string)), resultRoot!.Accept(PairProbeVisitor.Instance, state: false));

        // Result and stream stores are independent; pairs don't leak across them.
        Assert.Null(GeneratedDispatchRoots.FindResult(typeof(ClassMessage), typeof(int)));
        Assert.NotNull(GeneratedDispatchRoots.FindStream(typeof(ClassMessage), typeof(int)));
        Assert.Null(GeneratedDispatchRoots.FindStream(typeof(ClassMessage), typeof(string)));
    }

    [Fact]
    public void Add_IsIdempotent()
    {
        GeneratedDispatchRoots.AddMessage<ClassMessage>();
        var first = GeneratedDispatchRoots.FindMessage(typeof(ClassMessage));

        GeneratedDispatchRoots.AddMessage<ClassMessage>();

        Assert.Same(first, GeneratedDispatchRoots.FindMessage(typeof(ClassMessage)));
    }

    [Fact]
    public void FindMessage_UnknownType_ReturnsNull()
        => Assert.Null(GeneratedDispatchRoots.FindMessage(typeof(UnrootedMessage)));

    private sealed record UnrootedMessage : IMessage;
}
