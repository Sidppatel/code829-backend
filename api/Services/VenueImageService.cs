using System.Security.Cryptography;
using Contracts.DTOs.Images;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

public class VenueImageService(
    IFileStorageService fileStorage,
    IImageProcessingService imageProcessing,
    IVenueImageProcedures venueImageProc
) : IVenueImageService
{
    public async Task<AddVenueImageResponse> AddAsync(
        Guid venueId, Stream fileStream, string fileName,
        Guid uploadedById, string? altText = null, string? caption = null)
    {
        using var buffered = new MemoryStream();
        await fileStream.CopyToAsync(buffered);
        var checksum = ComputeSha256(buffered.ToArray());
        buffered.Position = 0;

        var variants = await imageProcessing.ProcessAsync(buffered, "venue");
        var detail = variants.First(v => v.Suffix == "");

        var baseKey = $"venue/{Guid.NewGuid()}";
        await Task.WhenAll(variants.Select(variant =>
            fileStorage.SaveWithKeyAsync(variant.Stream, $"{baseKey}{variant.Suffix}.webp", "image/webp")));
        foreach (var v in variants) v.Stream.Dispose();

        var result = await venueImageProc.AddAsync(
            venueId, baseKey, Path.GetFileName(fileName),
            detail.SizeBytes, detail.Width, detail.Height,
            uploadedById, "admin", altText, caption, "image/webp", checksum);

        Log.Information("[VenueImage] Added {ImageId} for venue {VenueId} (primary={IsPrimary})",
            result.ImageId, venueId, result.IsPrimary);

        return new AddVenueImageResponse(
            result.VenueImageId, result.ImageId,
            fileStorage.GetPublicUrl($"{baseKey}.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_card.webp"),
            result.SortOrder, result.IsPrimary);
    }

    public async Task<List<VenueImageDto>> ListAsync(Guid venueId)
    {
        var rows = await venueImageProc.ListAsync(venueId);
        return rows.Select(r => new VenueImageDto(
            r.VenueImageId, r.VenueId, r.ImageId,
            fileStorage.GetPublicUrl($"{r.StorageKey}.webp"),
            fileStorage.GetPublicUrl($"{r.StorageKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{r.StorageKey}_card.webp"),
            r.OriginalName, r.AltText, r.Caption, r.ContentType,
            r.SizeBytes, r.Width, r.Height,
            r.IsPrimary, r.SortOrder, r.CreatedAt)).ToList();
    }

    public async Task<bool> RemoveAsync(Guid venueId, Guid imageId)
    {
        var rows = await venueImageProc.ListAsync(venueId);
        var target = rows.FirstOrDefault(r => r.ImageId == imageId);
        if (target is null) return false;

        var removed = await venueImageProc.RemoveAsync(venueId, imageId);
        if (!removed) return false;

        foreach (var suffix in new[] { "", "_card", "_thumb" })
            await fileStorage.DeleteAsync($"{target.StorageKey}{suffix}.webp");

        Log.Information("[VenueImage] Removed {ImageId} from venue {VenueId}", imageId, venueId);
        return true;
    }

    public Task<bool> SetPrimaryAsync(Guid venueId, Guid imageId)
        => venueImageProc.SetPrimaryAsync(venueId, imageId);

    public Task ReorderAsync(Guid venueId, List<Guid> imageIds)
        => venueImageProc.ReorderAsync(venueId, imageIds.ToArray());

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
