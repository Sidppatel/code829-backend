namespace Db.Repositories.StoredProcedures;

public interface IImageProcedures
{
    Task<Guid> CreateImageAsync(string entityType, Guid entityId, string storageKey, string originalName, int sizeBytes, int width, int height, int sortOrder, Guid? uploadedBy = null, string? uploaderType = null, string? altText = null, string? caption = null, string? contentType = null, string? checksum = null, CancellationToken ct = default);
    Task<string> DeleteImageAsync(Guid imageId, CancellationToken ct = default);
    Task<string?> GetPrimaryImageKeyAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
