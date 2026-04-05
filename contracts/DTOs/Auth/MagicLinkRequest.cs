namespace Contracts.DTOs.Auth;

public record MagicLinkRequest(string Email, string? ReturnUrl = null);
