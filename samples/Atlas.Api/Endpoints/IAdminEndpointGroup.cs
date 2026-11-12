using Hlibz.MinimalApi.Endpoints;

namespace Atlas.Api.Endpoints;

/// <summary>
/// An admin route group. A group convention in Program.cs puts every one in the admin OpenAPI
/// document; the rest go in the public one.
/// </summary>
public interface IAdminEndpointGroup : IEndpointGroup;
