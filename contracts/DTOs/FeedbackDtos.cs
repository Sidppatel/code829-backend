namespace Contracts.DTOs;

public record SubmitFeedbackRequest(
    string Name,
    string? Email,
    string Type,
    string Message,
    int Rating,
    string? Diagnostics = null,
    string? PageUrl = null,
    string? StepsToReproduce = null
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
