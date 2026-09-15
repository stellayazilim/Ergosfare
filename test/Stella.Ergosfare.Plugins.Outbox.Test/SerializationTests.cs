using Stella.Ergosfare.Plugins.Outbox;

namespace OutboxTests;

[OutboxMessage]
public sealed partial class InventoryChanged
{
    public required string Name { get; init; }
    public Guid Id { get; set; }
    public decimal Price { get; set; }
    public bool Active { get; set; }
    public int? Count { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateTime Date { get; set; }
    public int[] Quantities { get; set; } = [];
    public List<string?> Tags { get; set; } = [];
}

[Trait("Category", "Unit")]
public sealed class SerializationTests
{
    [Fact]
    public async Task GeneratedCodec_RoundTripsPocoPrimitivesAndCollections_WithoutJsonContext()
    {
        var options = new OutboxOptions();
        var registration = options.Find(typeof(InventoryChanged));
        var input = new InventoryChanged
        {
            Name = "Türkçe \"ürün\"\n", Id = Guid.NewGuid(), Price = 12.34m, Active = true,
            Count = null, Notes = null, Timestamp = DateTimeOffset.UtcNow, Date = DateTime.UtcNow,
            Quantities = [1, 2, 3], Tags = ["tag", null]
        };
        var bytes = registration.Serialize(input);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"Price\":12.34", json);
        Assert.Contains("\"Count\":null", json);
        // Round-trip through the same adapter the worker uses, with a capturing typed mediator.
        var mediator = new CaptureMediator();
        await registration.DispatchAsync(bytes, mediator, default);
        var restored = Assert.IsType<OutboxEvent<InventoryChanged>>(mediator.Message).Message;
        Assert.Equal(input.Name, restored.Name);
        Assert.Equal(input.Id, restored.Id);
        Assert.Equal(input.Price, restored.Price);
        Assert.Equal(input.Active, restored.Active);
        Assert.Null(restored.Count);
        Assert.Null(restored.Notes);
        Assert.Equal(input.Timestamp, restored.Timestamp);
        Assert.Equal(input.Date, restored.Date);
        Assert.Equal(input.Quantities, restored.Quantities);
        Assert.Equal(input.Tags, restored.Tags);
    }

    private sealed class CaptureMediator : Stella.Ergosfare.Events.Abstractions.IEventMediator
    {
        public object? Message;
        public ValueTask PublishAsync<T>(T message, Stella.Ergosfare.Core.Abstractions.GroupSet groups,
            CancellationToken cancellationToken = default) where T : notnull
        {
            Message = message;
            return ValueTask.CompletedTask;
        }
        public ValueTask PublishAsync(Stella.Ergosfare.Events.Abstractions.IEvent message,
            Stella.Ergosfare.Core.Abstractions.GroupSet groups, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Expected typed dispatch");
        public ValueTask PublishAsync(Stella.Ergosfare.Events.Abstractions.IEvent message,
            Stella.Ergosfare.Core.Abstractions.ErgosfareContext context,
            Stella.Ergosfare.Core.Abstractions.GroupSet? groups = null)
            => throw new InvalidOperationException("Expected typed dispatch");
    }
}
