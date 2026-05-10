using System.Security.Cryptography;
using Contracts.DTOs.Images;
using Db.Entities;
using Db.Repositories;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Api.Services;

public class ImageService(
    IFileStorageService fileStorage,
    IImageProcessingService imageProcessing,
    IImageRepository imageRepo,
    IUserProcedures userProc,
    IBusinessUserProcedures businessUserProc,
    IHttpClientFactory httpClientFactory
) : IImageService
{
    private const long MaxRemoteImageBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan RemoteImageTimeout = TimeSpan.FromSeconds(10);

    public async Task<ImageUploadResponse> UploadAsync(
        Stream fileStream, string fileName, string entityType, Guid entityId,
        Guid? uploadedById, string? uploaderType = null,
        string? altText = null, string? caption = null, string tag = "Generic")
    {
        using var buffered = new MemoryStream();
        await fileStream.CopyToAsync(buffered);
        var checksum = ComputeSha256(buffered.ToArray());
        buffered.Position = 0;

        var variants = await imageProcessing.ProcessAsync(buffered, entityType);
        var detailVariant = variants.First(v => v.Suffix == "");

        var baseKey = $"{entityType}/{Guid.NewGuid()}";

        await Task.WhenAll(variants.Select(variant =>
            fileStorage.SaveWithKeyAsync(variant.Stream, $"{baseKey}{variant.Suffix}.webp", "image/webp")));

        var existing = await imageRepo.GetByEntityAsync(entityType, entityId);
        var sortOrder = existing.Count;

        var image = new Image
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            StorageKey = baseKey,
            Tag = tag,
            OriginalName = Path.GetFileName(fileName),
            SizeBytes = detailVariant.SizeBytes,
            Width = detailVariant.Width,
            Height = detailVariant.Height,
            SortOrder = sortOrder,
            UploadedById = uploadedById,
            UploaderType = uploaderType,
            AltText = altText,
            Caption = caption,
            ContentType = "image/webp",
            Checksum = checksum
        };

        await imageRepo.AddAsync(image);
        await imageRepo.SaveChangesAsync();

        Log.Information("[Image] Uploaded {EntityType}/{EntityId} ({Variants} variants)", entityType, entityId, variants.Count);

        foreach (var v in variants) v.Stream.Dispose();

        var storageKey = $"{baseKey}.webp";
        return new ImageUploadResponse(
            image.Id,
            storageKey,
            fileStorage.GetPublicUrl(storageKey),
            fileStorage.GetPublicUrl($"{baseKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_card.webp"),
            sortOrder == 0
        );
    }

    public async Task<List<ImageDto>> GetByEntityAsync(string entityType, Guid entityId)
    {
        var images = await imageRepo.GetByEntityAsync(entityType, entityId);
        return images.Take(50).Select(MapToDto).ToList();
    }

    public async Task<bool> DeleteAsync(Guid imageId)
    {
        var image = await imageRepo.GetByIdAsync(imageId);
        if (image is null) return false;

        var suffixes = GetSuffixes(image.EntityType);
        foreach (var suffix in suffixes)
            await fileStorage.DeleteAsync($"{image.StorageKey}{suffix}.webp");

        await imageRepo.DeleteAsync(image);
        await imageRepo.SaveChangesAsync();

        Log.Information("[Image] Deleted image {ImageId}", imageId);
        return true;
    }

    public async Task SetPrimaryAsync(Guid imageId)
    {
        var image = await imageRepo.GetByIdAsync(imageId);
        if (image is null) return;

        var all = await imageRepo.GetByEntityAsync(image.EntityType, image.EntityId);
        var target = all.FirstOrDefault(x => x.Id == imageId);
        if (target is null) return;

        var oldOrder = target.SortOrder;
        if (oldOrder == 0) return;

        foreach (var img in all.Where(x => x.SortOrder < oldOrder))
            img.SortOrder++;

        target.SortOrder = 0;
        await imageRepo.SaveChangesAsync();
    }

    public async Task ReorderAsync(string entityType, Guid entityId, List<Guid> imageIds)
    {
        var images = await imageRepo.GetByEntityAsync(entityType, entityId);
        for (var i = 0; i < imageIds.Count; i++)
        {
            var img = images.FirstOrDefault(x => x.Id == imageIds[i]);
            if (img is not null) img.SortOrder = i;
        }
        await imageRepo.SaveChangesAsync();
    }

    public async Task<string> ReplaceImageAsync(Guid ownerId, string uploaderType, IFormFile file)
    {
        var result = await UploadAsync(
            file.OpenReadStream(), file.FileName,
            entityType: uploaderType, entityId: ownerId,
            uploadedById: ownerId, uploaderType: uploaderType,
            tag: "ProfilePic");

        Guid? oldImageId = uploaderType == "business_user"
            ? await businessUserProc.SetBusinessUserImageAsync(ownerId, result.ImageId)
            : await userProc.SetUserImageAsync(ownerId, result.ImageId);

        if (oldImageId.HasValue)
            await DeleteAsync(oldImageId.Value);

        return result.StorageKey;
    }

    public async Task DeleteImageAsync(Guid ownerId, string uploaderType)
    {
        Guid? oldImageId = uploaderType == "business_user"
            ? await businessUserProc.ClearBusinessUserImageAsync(ownerId)
            : await userProc.ClearUserImageAsync(ownerId);

        if (oldImageId.HasValue)
            await DeleteAsync(oldImageId.Value);
    }

    public async Task<string?> IngestFromUrlAsync(string url, Guid userId, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Warning("[Image] IngestFromUrl rejected non-https URL for user {UserId}", userId);
            return null;
        }

        var client = httpClientFactory.CreateClient("remote-image");
        client.Timeout = RemoteImageTimeout;

        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("[Image] IngestFromUrl HTTP {Status} for user {UserId}", (int)response.StatusCode, userId);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("[Image] IngestFromUrl bad content-type '{ContentType}' for user {UserId}", contentType, userId);
                return null;
            }

            if (response.Content.Headers.ContentLength is long len && len > MaxRemoteImageBytes)
            {
                Log.Warning("[Image] IngestFromUrl payload {Bytes} exceeds limit for user {UserId}", len, userId);
                return null;
            }

            await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            long total = 0;
            while ((read = await sourceStream.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > MaxRemoteImageBytes)
                {
                    Log.Warning("[Image] IngestFromUrl streamed payload exceeded limit for user {UserId}", userId);
                    return null;
                }
                await bounded.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            bounded.Position = 0;

            var fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "remote-image";

            var result = await UploadAsync(
                bounded, fileName,
                entityType: "user", entityId: userId,
                uploadedById: userId, uploaderType: "user",
                tag: "ProfilePic");

            var oldImageId = await userProc.SetUserImageAsync(userId, result.ImageId, ct);
            if (oldImageId.HasValue && oldImageId.Value != result.ImageId)
                await DeleteAsync(oldImageId.Value);

            return result.StorageKey;
        }
        catch (TaskCanceledException ex)
        {
            Log.Warning(ex, "[Image] IngestFromUrl timed out for user {UserId}", userId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "[Image] IngestFromUrl HTTP error for user {UserId}", userId);
            return null;
        }
    }

    private ImageDto MapToDto(Image i) => new(
        i.Id,
        i.EntityType,
        i.EntityId,
        fileStorage.GetPublicUrl($"{i.StorageKey}.webp"),
        fileStorage.GetPublicUrl($"{i.StorageKey}_thumb.webp"),
        fileStorage.GetPublicUrl($"{i.StorageKey}_card.webp"),
        i.OriginalName,
        i.SizeBytes,
        i.Width,
        i.Height,
        i.SortOrder == 0,
        i.SortOrder,
        i.CreatedAt,
        i.AltText,
        i.Caption,
        i.ContentType
    );

    private static string[] GetSuffixes(string entityType) => entityType switch
    {
        "user" or "platform" => ["", "_thumb"],
        _ => ["", "_card", "_thumb"]
    };

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
