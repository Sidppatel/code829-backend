namespace Db.Repositories.StoredProcedures;

public interface ISponsorProcedures
{
    Task<Guid> CreateSponsorAsync(string name, string slug, string? imagePath, string metaJson, CancellationToken ct = default);
    Task UpdateSponsorAsync(Guid id, string? name, string? slug, string? imagePath, string? metaJson, CancellationToken ct = default);
    Task DeleteSponsorAsync(Guid id, CancellationToken ct = default);
    Task SetEventSponsorsAsync(Guid eventId, string linksJson, CancellationToken ct = default);
}
