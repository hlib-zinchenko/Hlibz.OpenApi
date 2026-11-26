namespace Hlibz.OpenApi.Results.Tests;

#pragma warning disable CA1815 // A union has no equality members of its own.
public union GetOrderError(OrderNotFound);

public union CancelOrderError(OrderNotFound, OrderClosed, OrderPaid);
#pragma warning restore CA1815
