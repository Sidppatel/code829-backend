using Db.Entities.Views;

namespace Db.Repositories.StoredProcedures;

public record AddPlatformImageResult(Guid ImageId, Guid PlatformImageId, int SortOrder, bool IsPrimary);

public interface IPlatformImageProcedures
{
    Task<AddPlatformImageResult> AddAsync(
        string storageKey, string originalName,
        int sizeBytes, int width, int height,
        Guid? uploadedBy = null, string? tag = null, string? uploaderType = null,
        string? altText = null, string? caption = null,
        string? contentType = null, string? checksum = null,
        CancellationToken ct = default);

    Task<bool> RemoveAsync(Guid imageId, CancellationToken ct = default);
    Task<bool> SetPrimaryAsync(Guid imageId, CancellationToken ct = default);
    Task ReorderAsync(Guid[] imageIds, CancellationToken ct = default);
    Task<List<PlatformImageView>> ListAsync(string? tag = null, CancellationToken ct = default);
}
