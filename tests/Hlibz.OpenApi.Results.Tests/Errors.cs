namespace Hlibz.OpenApi.Results.Tests;

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

public sealed record OrderDto(int Id);
