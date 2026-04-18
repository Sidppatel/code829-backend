namespace Contracts.DTOs.Logs;

public record EmailLogDto(
    Guid EmailLogId,
    string Recipient,
    string Subject,
    string Body,
    string? Status,
    DateTime Timestamp
);
