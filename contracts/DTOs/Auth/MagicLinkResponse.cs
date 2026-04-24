namespace Contracts.DTOs.Auth;

/// <summary>
/// Response after requesting a magic link. Only a message is returned — the
/// raw token is never included. Dev mode logs the verify URL via Serilog
/// (Debug level) so local QA has access without leaking tokens over the wire.
/// </summary>
public record MagicLinkResponse(
    string Message
);
