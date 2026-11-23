using System.ComponentModel;
using System.Text.Json.Serialization;

using Hlibz.Paging;

using Microsoft.AspNetCore.Mvc;

namespace Hlibz.OpenApi.Paging.Tests;

public sealed class OrderDto
{
    [Sortable]
    public int Id { get; init; }

    [Sortable]
    public string CustomerName { get; init; } = string.Empty;

    [Sortable(IsDefault = true, DefaultDirection = ListSortDirection.Descending)]
    public DateTime CreatedAt { get; init; }
}

public sealed class OrdersQuery
{
    [FromQuery(Name = "sort")]
    [SortableBy(typeof(OrderDto), NamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    public string? Sort { get; init; }
}

public sealed record OrdersSearch(
    [SortableBy(typeof(OrderDto))] string? Sort,
    string? Text);
