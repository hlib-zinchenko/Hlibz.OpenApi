namespace Atlas.Api.Endpoints;

/// <summary>The sample's authorization policies.</summary>
public static class Policies
{
    /// <summary>Any signed-in user.</summary>
    public const string Member = "Member";

    /// <summary>A user with the <c>admin</c> role.</summary>
    public const string Admin = "Admin";
}
