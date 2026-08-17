using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Test.Results;

/// <summary>
/// The zero-allocation result carriers and the declarative adapter binding: native
/// <see cref="Result"/>/<see cref="Result{TValue}"/> bind to their built-in adapters with
/// no annotation, an annotated message binds its declared adapter for the matching slot
/// only, and everything else resolves to no adapter at all.
/// </summary>
/// <remarks>
/// The annotation and fallback tiers are answered from the generated table, so the fixtures
/// below are entered into it by hand — these messages are private to this file and no
/// generated registration could name them. What each entry stands for is what the generator
/// would have written for the same declaration.
/// </remarks>
public class ResultAndAdapterBindingTests
{
    static ResultAndAdapterBindingTests()
    {
        // [ResultAdapter(typeof(CustomOutcomeAdapter))] on a message declaring CustomOutcome.
        GeneratedDispatchRoots.AddResultAdapter<AnnotatedMessage, CustomOutcome, CustomOutcomeAdapter>();

        // [IgnoreResultAdapter], own and inherited — the base walk happens in the generator,
        // so the derived message carries its own entry.
        GeneratedDispatchRoots.AddIgnoredResultAdapter<IgnoredMessage>();
        GeneratedDispatchRoots.AddIgnoredResultAdapter<DerivedFromIgnoredMessage>();

        // UseDefaultResultAdapter(typeof(CustomOutcomeAdapter)) over a CustomOutcome slot,
        // and typeof(BoxAdapter<>) closed over a Box<int> slot.
        GeneratedDispatchRoots.AddDefaultResultAdapter<CustomOutcome, CustomOutcomeAdapter>();
        GeneratedDispatchRoots.AddDefaultResultAdapter<Box<int>, BoxAdapter<int>>();

        GeneratedDispatchRoots.SealResultAdapters();
    }

    private sealed record PlainMessage;

    private sealed class CustomOutcome
    {
        public Exception? Error { get; init; }
    }

    private sealed class CustomOutcomeAdapter : IResultAdapter<CustomOutcome>
    {
        public bool TryGetException(in CustomOutcome result, out Exception? exception)
        {
            exception = result.Error;
            return exception is not null;
        }
    }

    [ResultAdapter(typeof(CustomOutcomeAdapter))]
    private sealed record AnnotatedMessage;

    [IgnoreResultAdapter]
    private sealed record IgnoredMessage;

    [IgnoreResultAdapter]
    private record IgnoredBaseMessage;

    private sealed record DerivedFromIgnoredMessage : IgnoredBaseMessage;

    // The parameter is the scenario: the type exists to be generic, not to use T.
    // ReSharper disable once UnusedTypeParameter
    private sealed class Box<T>
    {
        public Exception? Error { get; init; }
    }

    private sealed class BoxAdapter<T> : IResultAdapter<Box<T>>
    {
        public bool TryGetException(in Box<T> result, out Exception? exception)
        {
            exception = result.Error;
            return exception is not null;
        }
    }

    private sealed class StubProvider(DefaultResultAdapter? defaultAdapter) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(DefaultResultAdapter) ? defaultAdapter : null;
    }

    [Fact]
    public void ResultCarriers_ExposeSuccessAndFailure()
    {
        var ok = Result.Ok();
        Assert.True(ok.IsSuccess);
        Assert.Null(ok.Exception);

        var failure = new InvalidOperationException("boom");
        var fail = Result.Fail(failure);
        Assert.False(fail.IsSuccess);
        Assert.Same(failure, fail.Exception);

        Result<int> okValue = 42; // implicit success conversion
        Assert.True(okValue.IsSuccess);
        Assert.Equal(42, okValue.Value);
        Assert.True(okValue.TryGetValue(out var value) && value == 42);

        var failValue = Result<int>.Fail(failure);
        Assert.False(failValue.IsSuccess);
        Assert.Same(failure, failValue.Exception);
        Assert.False(failValue.TryGetValue(out _));
        Assert.Equal(0, failValue.GetValueOrDefault());
        Assert.Throws<InvalidOperationException>(() => failValue.Value);
    }

    [Fact]
    public void NativeCarriers_BindTheirBuiltInAdapters()
    {
        var voidAdapter = ResultAdapterBinding.For<PlainMessage, Result>();
        Assert.NotNull(voidAdapter);

        var failure = new InvalidOperationException("boom");
        Assert.True(voidAdapter.TryGetException(Result.Fail(failure), out var carried));
        Assert.Same(failure, carried);
        Assert.False(voidAdapter.TryGetException(Result.Ok(), out _));

        var valueAdapter = ResultAdapterBinding.For<PlainMessage, Result<string>>();
        Assert.NotNull(valueAdapter);
        Assert.True(valueAdapter.TryGetException(Result<string>.Fail(failure), out carried));
        Assert.Same(failure, carried);
        Assert.False(valueAdapter.TryGetException(Result<string>.Ok("ok"), out _));
    }

    [Fact]
    public void UnannotatedForeignResult_BindsNothing()
        => Assert.Null(ResultAdapterBinding.For<PlainMessage, CustomOutcome>());

    [Fact]
    public void NativeAdapters_MaterializeFailuresIntoFailedCarriers()
    {
        var failure = new InvalidOperationException("boom");

        var voidMaterializer = Assert.IsAssignableFrom<IResultMaterializer<Result>>(
            ResultAdapterBinding.For<PlainMessage, Result>());
        var materialized = voidMaterializer.Materialize(failure);
        Assert.False(materialized.IsSuccess);
        Assert.Same(failure, materialized.Exception);

        var valueMaterializer = Assert.IsAssignableFrom<IResultMaterializer<Result<string>>>(
            ResultAdapterBinding.For<PlainMessage, Result<string>>());
        var materializedValue = valueMaterializer.Materialize(failure);
        Assert.False(materializedValue.IsSuccess);
        Assert.Same(failure, materializedValue.Exception);

        // A foreign adapter without the facet stays non-materializable: real throws keep
        // the classic unhandled-rethrow contract for its carrier.
        // Asserting the facet is absent — the check being "suspicious" is the assertion.
        // ReSharper disable once SuspiciousTypeConversion.Global
        Assert.False(ResultAdapterBinding.For<AnnotatedMessage, CustomOutcome>() is IResultMaterializer<CustomOutcome>);
    }

    [Fact]
    public void AnnotatedMessage_BindsItsAdapterForTheMatchingSlotOnly()
    {
        var bound = ResultAdapterBinding.For<AnnotatedMessage, CustomOutcome>();
        Assert.IsType<CustomOutcomeAdapter>(bound);

        var failure = new InvalidOperationException("boom");
        Assert.True(bound.TryGetException(new CustomOutcome { Error = failure }, out var carried));
        Assert.Same(failure, carried);

        // The annotation targets the declared result; other slots of the same message
        // must resolve past it.
        Assert.Null(ResultAdapterBinding.For<AnnotatedMessage, string>());
        Assert.NotNull(ResultAdapterBinding.For<AnnotatedMessage, Result>());
    }

    [Fact]
    public void IgnoredMessage_SuppressesEveryTier()
    {
        var defaultAdapter = new DefaultResultAdapter(typeof(CustomOutcomeAdapter));
        var provider = new StubProvider(defaultAdapter);

        // The opt-out beats the native carriers on the attribute tiers...
        Assert.Null(ResultAdapterBinding.For<IgnoredMessage, Result>());
        Assert.Null(ResultAdapterBinding.For<IgnoredMessage, Result<string>>());

        // ...and beats the container's default on the effective resolution, including
        // when inherited from a base message type.
        Assert.Null(ResultAdapterBinding.For<IgnoredMessage, CustomOutcome>(provider));
        Assert.Null(ResultAdapterBinding.For<DerivedFromIgnoredMessage, CustomOutcome>(provider));
    }

    [Fact]
    public void DefaultAdapter_IsTheLastTierOfTheEffectiveResolution()
    {
        var provider = new StubProvider(new DefaultResultAdapter(typeof(CustomOutcomeAdapter)));

        // An unannotated foreign slot falls back to the configured default...
        Assert.IsType<CustomOutcomeAdapter>(ResultAdapterBinding.For<PlainMessage, CustomOutcome>(provider));

        // ...while the annotation and native tiers stay in front of it, and a slot the
        // default does not serve stays adapterless.
        Assert.IsType<CustomOutcomeAdapter>(ResultAdapterBinding.For<AnnotatedMessage, CustomOutcome>(provider));
        Assert.IsType<ResultExceptionAdapter<int>>(ResultAdapterBinding.For<PlainMessage, Result<int>>(provider));
        Assert.Null(ResultAdapterBinding.For<PlainMessage, string>(provider));

        // No default configured → the classic try/catch world, untouched.
        Assert.Null(ResultAdapterBinding.For<PlainMessage, CustomOutcome>(new StubProvider(null)));
    }

    [Fact]
    public void OpenGenericDefaultAdapter_ServesTheSlotItWasClosedOver()
    {
        var provider = new StubProvider(new DefaultResultAdapter(typeof(BoxAdapter<>)));

        var bound = ResultAdapterBinding.For<PlainMessage, Box<int>>(provider);
        Assert.IsType<BoxAdapter<int>>(bound);

        var failure = new InvalidOperationException("boom");
        Assert.True(bound.TryGetException(new Box<int> { Error = failure }, out var carried));
        Assert.Same(failure, carried);

        // One instance per slot, built where the definition was closed; a slot the
        // definition was never closed over resolves to nothing.
        Assert.Same(bound, ResultAdapterBinding.For<PlainMessage, Box<int>>(provider));
        Assert.Null(ResultAdapterBinding.For<PlainMessage, Box<string>>(provider));
    }

    [Fact]
    public void DefaultAdapter_RejectsUnusableTypes()
    {
        // Not an adapter at all, and an adapter without a public parameterless ctor.
        Assert.Throws<ArgumentException>(() => new DefaultResultAdapter(typeof(string)));
        Assert.Throws<ArgumentException>(() => new DefaultResultAdapter(typeof(DependentAdapter)));
    }

    /// <summary>
    ///     An adapter with no parameterless constructor. The argument is never read — its
    ///     only job is to make the type one <see cref="DefaultResultAdapter"/> cannot
    ///     activate, which is what the assertion above pins.
    /// </summary>
    private sealed class DependentAdapter(string dependency) : IResultAdapter<CustomOutcome>
    {
        private string Dependency { get; } = dependency;

        public override string ToString() => Dependency;

        public bool TryGetException(in CustomOutcome result, out Exception? exception)
        {
            exception = result.Error;
            return exception is not null;
        }
    }
}
