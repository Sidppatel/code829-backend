using Contracts.DTOs.Images;
using Db.Entities;
using Db.Repositories;
using Serilog;

namespace Api.Services;

public class ImageService(
    IFileStorageService fileStorage,
    IImageProcessingService imageProcessing,
    IImageRepository imageRepo
) : IImageService
{
    public async Task<ImageUploadResponse> UploadAsync(
        Stream fileStream, string fileName, string entityType, Guid entityId, Guid uploadedById)
    {
        var variants = await imageProcessing.ProcessAsync(fileStream, entityType);
        var detailVariant = variants.First(v => v.Suffix == "");

        // Upload all variants to storage
        var baseKey = $"{entityType}/{Guid.NewGuid()}";
        foreach (var variant in variants)
        {
            var key = $"{baseKey}{variant.Suffix}.webp";
            await fileStorage.SaveWithKeyAsync(variant.Stream, key, "image/webp");
        }

        // Check if this is the first image for the entity
        var existing = await imageRepo.GetByEntityAsync(entityType, entityId);
        var isPrimary = existing.Count == 0;

        var image = new Image
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            StorageKey = baseKey,
            OriginalName = Path.GetFileName(fileName),
            SizeBytes = detailVariant.SizeBytes,
            Width = detailVariant.Width,
            Height = detailVariant.Height,
            IsPrimary = isPrimary,
            SortOrder = existing.Count,
            UploadedById = uploadedById
        };

        await imageRepo.AddAsync(image);
        await imageRepo.SaveChangesAsync();

        Log.Information("[Image] Uploaded {EntityType}/{EntityId} ({Variants} variants)", entityType, entityId, variants.Count);

        // Dispose variant streams
        foreach (var v in variants) v.Stream.Dispose();

        var storageKey = $"{baseKey}.webp";
        return new ImageUploadResponse(
            image.Id,
            storageKey,
            fileStorage.GetPublicUrl(storageKey),
            fileStorage.GetPublicUrl($"{baseKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_card.webp"),
            isPrimary
        );
    }

    public async Task<List<ImageDto>> GetByEntityAsync(string entityType, Guid entityId)
    {
        var images = await imageRepo.GetByEntityAsync(entityType, entityId);
        return images.Take(50).Select(i => MapToDto(i)).ToList();
    }

    public async Task<bool> DeleteAsync(Guid imageId)
    {
        var image = await imageRepo.GetByIdAsync(imageId);
        if (image is null) return false;

        // Delete all variant files from storage
        var suffixes = GetSuffixes(image.EntityType);
        foreach (var suffix in suffixes)
        {
            await fileStorage.DeleteAsync($"{image.StorageKey}{suffix}.webp");
        }

        var wasPrimary = image.IsPrimary;
        var entityType = image.EntityType;
        var entityId = image.EntityId;

        await imageRepo.DeleteAsync(image);
        await imageRepo.SaveChangesAsync();

        // If deleted image was primary, promote the next one
        if (wasPrimary)
        {
            var remaining = await imageRepo.GetByEntityAsync(entityType, entityId);
            if (remaining.Count > 0)
            {
                remaining[0].IsPrimary = true;
                await imageRepo.SaveChangesAsync();
            }
        }

        Log.Information("[Image] Deleted image {ImageId}", imageId);
        return true;
    }

    public async Task SetPrimaryAsync(Guid imageId)
    {
        var image = await imageRepo.GetByIdAsync(imageId);
        if (image is null) return;

        // Unset current primary
        var currentPrimary = await imageRepo.GetPrimaryAsync(image.EntityType, image.EntityId);
        if (currentPrimary is not null)
        {
            currentPrimary.IsPrimary = false;
        }

        image.IsPrimary = true;
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
        i.IsPrimary,
        i.SortOrder,
        i.CreatedAt
    );

    private static string[] GetSuffixes(string entityType) => entityType switch
    {
        "user" or "platform" => ["", "_thumb"],
        _ => ["", "_card", "_thumb"]
    };
}
