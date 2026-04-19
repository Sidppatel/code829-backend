namespace Contracts.DTOs.Logs;

public record EmailLogDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid EmailLogId,
    string Recipient,
    string Subject,
    string Body,
    string? Status,
    DateTime Timestamp
);
