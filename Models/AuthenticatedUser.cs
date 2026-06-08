namespace Hub.Models;

public sealed class AuthenticatedUser
{
    public required string DisplayName { get; init; }

    public required string SamAccountName { get; init; }

    public required string SidValue { get; init; }
}
