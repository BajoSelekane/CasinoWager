using System.ComponentModel.DataAnnotations;

namespace OT.Assessment.API.Authentication;

/// <summary>Credentials and profile details for creating a player account.</summary>
public sealed record RegisterAccountRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(3), MaxLength(100), RegularExpression(@"^(?=.*\S).+$")]
    public string Username { get; init; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>Credentials for signing in to an existing account.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>Account details and a bearer token for authenticated API requests.</summary>
public sealed record AuthenticationResponse(
    Guid AccountId,
    string Email,
    string Username,
    string AccessToken,
    DateTimeOffset ExpiresAt);

/// <summary>Public profile details for the authenticated account.</summary>
public sealed record AccountResponse(Guid AccountId, string Email, string Username);
