using Contracts.DTOs.Images;
using Microsoft.AspNetCore.Http;

namespace Api.Services;

public interface IImageService
{
    Task<ImageUploadResponse> UploadAsync(
        Stream fileStream, string fileName, string entityType, Guid entityId,
        Guid? uploadedById, string? uploaderType = null,
        string? altText = null, string? caption = null, string tag = "Generic");
    Task<List<ImageDto>> GetByEntityAsync(string entityType, Guid entityId);
    Task<bool> DeleteAsync(Guid imageId);
    Task SetPrimaryAsync(Guid imageId);
    Task ReorderAsync(string entityType, Guid entityId, List<Guid> imageIds);
    Task<string> ReplaceImageAsync(Guid ownerId, string uploaderType, IFormFile file);
    Task DeleteImageAsync(Guid ownerId, string uploaderType);
    Task<string?> IngestFromUrlAsync(string url, Guid userId, CancellationToken ct = default);
}
