using Contracts.DTOs.Images;

namespace Api.Services;

public interface IPlatformImageService
{
    Task<AddPlatformImageResponse> AddAsync(
        Stream fileStream, string fileName, Guid uploadedById,
        string? tag = null, string? altText = null, string? caption = null);

    Task<List<PlatformImageDto>> ListAsync(string? tag = null);
    Task<bool> RemoveAsync(Guid imageId);
    Task<bool> SetPrimaryAsync(Guid imageId);
    Task ReorderAsync(List<Guid> imageIds);
}
