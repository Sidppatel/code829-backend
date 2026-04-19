using Contracts.DTOs.Images;

namespace Api.Services;

public interface IImageService
{
    Task<ImageUploadResponse> UploadAsync(
        Stream fileStream, string fileName, string entityType, Guid entityId,
        Guid? uploadedById, string? uploaderType = null,
        string? altText = null, string? caption = null);
    Task<List<ImageDto>> GetByEntityAsync(string entityType, Guid entityId);
    Task<bool> DeleteAsync(Guid imageId);
    Task SetPrimaryAsync(Guid imageId);
    Task ReorderAsync(string entityType, Guid entityId, List<Guid> imageIds);
}
