namespace Contracts.DTOs.Logs;

public record EmailLogDto(
    Guid Id,
    string Recipient,
    string Subject,
    string Body,
    string? Status,
    DateTime Timestamp
);
