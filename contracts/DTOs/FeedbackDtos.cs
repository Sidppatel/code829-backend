namespace Contracts.DTOs;

public record SubmitFeedbackRequest(
    string Name,
    string? Email,
    string Type,
    string Message,
    int Rating,
    string? Diagnostics = null
);

public record FeedbackDto(
    Guid FeedbackId,
    string Name,
    string? Email,
    string Type,
    string Message,
    int Rating,
    Guid? UserId,
    string? UserName,
    DateTime CreatedAt,
    string? Diagnostics = null
);
