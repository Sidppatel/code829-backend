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
    IBusinessUserProcedures businessUserProc
) : IImageService
{
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
