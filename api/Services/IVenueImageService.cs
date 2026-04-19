using Contracts.DTOs.Images;

namespace Api.Services;

public interface IVenueImageService
{
    Task<AddVenueImageResponse> AddAsync(
        Guid venueId, Stream fileStream, string fileName,
        Guid uploadedById, string? altText = null, string? caption = null);

    Task<List<VenueImageDto>> ListAsync(Guid venueId);
    Task<bool> RemoveAsync(Guid venueId, Guid imageId);
    Task<bool> SetPrimaryAsync(Guid venueId, Guid imageId);
    Task ReorderAsync(Guid venueId, List<Guid> imageIds);
}
