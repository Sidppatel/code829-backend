namespace Contracts.DTOs.Images;

public record ImageDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Url,
    string ThumbnailUrl,
    string CardUrl,
    string? OriginalName,
    int SizeBytes,
    int Width,
    int Height,
    bool IsPrimary,
    int SortOrder,
    DateTime CreatedAt
);

public record ImageUploadResponse(
    Guid Id,
    string StorageKey,
    string Url,
    string ThumbnailUrl,
    string CardUrl,
    bool IsPrimary
);

public record ReorderImagesRequest(List<Guid> ImageIds);
