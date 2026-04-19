using Contracts.DTOs.Images;

namespace Api.Services;

public interface IEventImageService
{
    Task<AddEventImageResponse> AddAsync(
        Guid eventId, Stream fileStream, string fileName,
        Guid uploadedById, string? altText = null, string? caption = null);

    Task<List<EventImageDto>> ListAsync(Guid eventId);
    Task<bool> RemoveAsync(Guid eventId, Guid imageId);
    Task<bool> SetPrimaryAsync(Guid eventId, Guid imageId);
    Task ReorderAsync(Guid eventId, List<Guid> imageIds);
}
