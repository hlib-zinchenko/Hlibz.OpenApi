namespace Hlibz.OpenApi.Results.Tests;

public union GetOrderError(OrderNotFound);

public union CancelOrderError(OrderNotFound, OrderClosed, OrderPaid);

public union PlaceOrderError(OrderEmpty, OrderNotYours);

public union MoveOrderError(OrderStatusInvalid);
