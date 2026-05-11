namespace Contracts.DTOs.Auth;

public record GoogleSignInRequest(string? Credential = null, string? Code = null);
