namespace Db.Repositories.StoredProcedures;

public interface IImageProcedures
{
    Task<Guid> CreateImageAsync(string entityType, Guid entityId, string storageKey, string originalName, int sizeBytes, int width, int height, bool isPrimary, int sortOrder, Guid? uploadedBy = null, CancellationToken ct = default);
    Task<string> DeleteImageAsync(Guid imageId, CancellationToken ct = default);
}
