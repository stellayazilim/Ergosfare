using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// Unit tests for the data-carrier API of <see cref="ErgosfareExecutionContext"/> —
/// the context is a raw data carrier between handlers, so <c>Items</c>, <c>Set</c>,
/// <c>Has</c>, <c>Get</c> and <c>TryGet</c> are its primary contract.
/// </summary>
public class ExecutionContextDataTests
{
    private static ErgosfareExecutionContext CreateContext(IDictionary<object, object?>? items = null) =>
        new(items, CancellationToken.None);

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Items_ShouldBeCreatedLazily_WhenNotProvided()
    {
        var context = CreateContext();

        Assert.NotNull(context.Items);
        Assert.Empty(context.Items);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Set_ShouldStoreItem_AndHasShouldFindIt()
    {
        var context = CreateContext();

        context.Set("foo", "bar");

        Assert.True(context.Has("foo"));
        Assert.False(context.Has("missing"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Set_ShouldOverwriteExistingItem()
    {
        var context = CreateContext();

        context.Set("foo", "bar");
        context.Set("foo", "baz");

        Assert.Equal("baz", context.Get<string>("foo"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Get_ShouldReturnStoredItem()
    {
        var context = CreateContext();
        context.Set("foo", "bar");

        Assert.Equal("bar", context.Get<string>("foo"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Get_ShouldThrow_WhenItemMissing()
    {
        var context = CreateContext();

        Assert.Throws<InvalidOperationException>(() => context.Get<string>("missing"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TryGet_ShouldReturnItem_WhenPresent()
    {
        var context = CreateContext();
        context.Set("foo", "bar");

        var found = context.TryGet<string>("foo", out var item);

        Assert.True(found);
        Assert.Equal("bar", item);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TryGet_ShouldReturnFalse_WhenMissing()
    {
        var context = CreateContext();

        var found = context.TryGet<string>("missing", out var item);

        Assert.False(found);
        Assert.Null(item);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Items_ShouldShareProvidedDictionaryInstance()
    {
        var items = new Dictionary<object, object?>();
        var context = CreateContext(items);

        items.Add("foo", "bar");

        Assert.True(context.Has("foo"));
        Assert.Same(items, context.Items);
    }
}
