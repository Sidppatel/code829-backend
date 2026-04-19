using Db.Entities.Views;

namespace Db.Repositories.StoredProcedures;

public record AddEventImageResult(Guid ImageId, Guid EventImageId, int SortOrder, bool IsPrimary);

public interface IEventImageProcedures
{
    Task<AddEventImageResult> AddAsync(
        Guid eventId, string storageKey, string originalName,
        int sizeBytes, int width, int height,
        Guid? uploadedBy = null, string? uploaderType = null,
        string? altText = null, string? caption = null,
        string? contentType = null, string? checksum = null,
        CancellationToken ct = default);

    Task<bool> RemoveAsync(Guid eventId, Guid imageId, CancellationToken ct = default);
    Task<bool> SetPrimaryAsync(Guid eventId, Guid imageId, CancellationToken ct = default);
    Task ReorderAsync(Guid eventId, Guid[] imageIds, CancellationToken ct = default);
    Task<List<EventImageView>> ListAsync(Guid eventId, CancellationToken ct = default);
}
