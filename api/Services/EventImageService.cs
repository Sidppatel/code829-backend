using System.Security.Cryptography;
using Contracts.DTOs.Images;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

public class EventImageService(
    IFileStorageService fileStorage,
    IImageProcessingService imageProcessing,
    IEventImageProcedures eventImageProc
) : IEventImageService
{
    public async Task<AddEventImageResponse> AddAsync(
        Guid eventId, Stream fileStream, string fileName,
        Guid uploadedById, string? altText = null, string? caption = null)
    {
        using var buffered = new MemoryStream();
        await fileStream.CopyToAsync(buffered);
        var checksum = ComputeSha256(buffered.ToArray());
        buffered.Position = 0;

        var variants = await imageProcessing.ProcessAsync(buffered, "event");
        var detail = variants.First(v => v.Suffix == "");

        var baseKey = $"event/{Guid.NewGuid()}";
        await Task.WhenAll(variants.Select(variant =>
            fileStorage.SaveWithKeyAsync(variant.Stream, $"{baseKey}{variant.Suffix}.webp", "image/webp")));
        foreach (var v in variants) v.Stream.Dispose();

        var result = await eventImageProc.AddAsync(
            eventId, baseKey, Path.GetFileName(fileName),
            detail.SizeBytes, detail.Width, detail.Height,
            uploadedById, "admin",
            altText, caption, "image/webp", checksum);

        Log.Information("[EventImage] Added {ImageId} for event {EventId} (primary={IsPrimary})",
            result.ImageId, eventId, result.IsPrimary);

        return new AddEventImageResponse(
            result.EventImageId,
            result.ImageId,
            fileStorage.GetPublicUrl($"{baseKey}.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_card.webp"),
            result.SortOrder,
            result.IsPrimary);
    }

    public async Task<List<EventImageDto>> ListAsync(Guid eventId)
    {
        var rows = await eventImageProc.ListAsync(eventId);
        return rows.Select(r => new EventImageDto(
            r.EventImageId, r.EventId, r.ImageId,
            fileStorage.GetPublicUrl($"{r.StorageKey}.webp"),
            fileStorage.GetPublicUrl($"{r.StorageKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{r.StorageKey}_card.webp"),
            r.OriginalName, r.AltText, r.Caption, r.ContentType,
            r.SizeBytes, r.Width, r.Height,
            r.IsPrimary, r.SortOrder, r.CreatedAt)).ToList();
    }

    public async Task<bool> RemoveAsync(Guid eventId, Guid imageId)
    {

        var rows = await eventImageProc.ListAsync(eventId);
        var target = rows.FirstOrDefault(r => r.ImageId == imageId);
        if (target is null) return false;

        var removed = await eventImageProc.RemoveAsync(eventId, imageId);
        if (!removed) return false;

        foreach (var suffix in new[] { "", "_card", "_thumb" })
        {
            await fileStorage.DeleteAsync($"{target.StorageKey}{suffix}.webp");
        }

        Log.Information("[EventImage] Removed {ImageId} from event {EventId}", imageId, eventId);
        return true;
    }

    public Task<bool> SetPrimaryAsync(Guid eventId, Guid imageId)
        => eventImageProc.SetPrimaryAsync(eventId, imageId);

    public Task ReorderAsync(Guid eventId, List<Guid> imageIds)
        => eventImageProc.ReorderAsync(eventId, imageIds.ToArray());

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
