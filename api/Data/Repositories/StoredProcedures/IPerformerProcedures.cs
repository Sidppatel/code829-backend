namespace Db.Repositories.StoredProcedures;

public interface IPerformerProcedures
{
    Task<Guid> CreatePerformerAsync(string name, string slug, string? imagePath, string metaJson, CancellationToken ct = default);
    Task UpdatePerformerAsync(Guid id, string? name, string? slug, string? imagePath, string? metaJson, CancellationToken ct = default);
    Task DeletePerformerAsync(Guid id, CancellationToken ct = default);
    Task SetEventPerformersAsync(Guid eventId, string linksJson, CancellationToken ct = default);
}
