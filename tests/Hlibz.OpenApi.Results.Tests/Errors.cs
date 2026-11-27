using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hlibz.OpenApi.Results.Tests;

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Open,
    Paid,
}

/// <summary>A strongly-typed ID, written as its <see cref="Guid"/> by its own converter.</summary>
[JsonConverter(typeof(OrderIdConverter))]
public readonly record struct OrderId(Guid Value);

[Error("orders_1001", ResultStatus.NotFound, "The order wasn't found.")]
public sealed record OrderNotFound(int OrderId) : Error;

[Error("orders_1002", ResultStatus.Conflict, "The order is closed.")]
public sealed record OrderClosed : Error;

[Error("orders_1003", ResultStatus.Conflict, "The order has payments.")]
public sealed record OrderPaid(IReadOnlyList<int> PaymentIds) : Error;

[Error("orders_1004", ResultStatus.Error, "The order has no lines.")]
public sealed record OrderEmpty : Error;

[Error("orders_1005", ResultStatus.Forbidden, "Only the customer can place this order.")]
public sealed record OrderNotYours : Error;

[Error("orders_1006", ResultStatus.Conflict, "The order can't move to that status.")]
public sealed record OrderStatusInvalid(OrderId OrderId, OrderStatus From, OrderStatus? To) : Error;

public sealed record OrderDto(int Id);

public sealed record OrderStatusDto(OrderId Id, OrderStatus Status);

public sealed class OrderIdConverter : JsonConverter<OrderId>
{
    public override OrderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, OrderId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
