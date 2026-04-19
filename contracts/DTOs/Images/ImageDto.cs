namespace Contracts.DTOs.Images;

public record ImageDto(
    Guid ImageId,
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
    DateTime CreatedAt,
    string? AltText = null,
    string? Caption = null,
    string? ContentType = null
);

public record ImageUploadResponse(
    Guid ImageId,
    string StorageKey,
    string Url,
    string ThumbnailUrl,
    string CardUrl,
    bool IsPrimary
);

public record ReorderImagesRequest(List<Guid> ImageIds);

public record EventImageDto(
    Guid EventImageId,
    Guid EventId,
    Guid ImageId,
    string Url,
    string ThumbnailUrl,
    string CardUrl,
    string? OriginalName,
    string? AltText,
    string? Caption,
    string? ContentType,
    int SizeBytes,
    int Width,
    int Height,
    bool IsPrimary,
    int SortOrder,
    DateTime CreatedAt
);

public record AddEventImageResponse(
    Guid EventImageId,
    Guid ImageId,
    string Url,
    string ThumbnailUrl,
    string CardUrl,
    int SortOrder,
    bool IsPrimary
);

public record ReorderEventImagesRequest(List<Guid> ImageIds);
