namespace Contracts.DTOs.Auth;

/// <summary>
/// Response after requesting a magic link.
/// In development mode, the token is included in the response for easy testing.
/// In production, only the message is returned.
/// </summary>
public record MagicLinkResponse(
    string Message,
    string? Token = null
);
