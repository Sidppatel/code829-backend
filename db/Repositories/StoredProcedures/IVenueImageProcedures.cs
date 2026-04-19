using Db.Entities.Views;

namespace Db.Repositories.StoredProcedures;

public record AddVenueImageResult(Guid ImageId, Guid VenueImageId, int SortOrder, bool IsPrimary);

public interface IVenueImageProcedures
{
    Task<AddVenueImageResult> AddAsync(
        Guid venueId, string storageKey, string originalName,
        int sizeBytes, int width, int height,
        Guid? uploadedBy = null, string? uploaderType = null,
        string? altText = null, string? caption = null,
        string? contentType = null, string? checksum = null,
        CancellationToken ct = default);

    Task<bool> RemoveAsync(Guid venueId, Guid imageId, CancellationToken ct = default);
    Task<bool> SetPrimaryAsync(Guid venueId, Guid imageId, CancellationToken ct = default);
    Task ReorderAsync(Guid venueId, Guid[] imageIds, CancellationToken ct = default);
    Task<List<VenueImageView>> ListAsync(Guid venueId, CancellationToken ct = default);
}
