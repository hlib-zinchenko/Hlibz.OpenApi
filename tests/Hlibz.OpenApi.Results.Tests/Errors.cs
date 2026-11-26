namespace Hlibz.OpenApi.Results.Tests;

[Error("orders_1001", ResultStatus.NotFound, "The order wasn't found.")]
public sealed record OrderNotFound(int OrderId) : Error;

[Error("orders_1002", ResultStatus.Conflict, "The order is closed.")]
public sealed record OrderClosed : Error;

[Error("orders_1003", ResultStatus.Conflict, "The order has payments.")]
public sealed record OrderPaid(IReadOnlyList<int> PaymentIds) : Error;

public sealed record OrderDto(int Id);
