using System.Security.Cryptography;
using Contracts.DTOs.Images;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

public class PlatformImageService(
    IFileStorageService fileStorage,
    IImageProcessingService imageProcessing,
    IPlatformImageProcedures platformImageProc
) : IPlatformImageService
{
    public async Task<AddPlatformImageResponse> AddAsync(
        Stream fileStream, string fileName, Guid uploadedById,
        string? tag = null, string? altText = null, string? caption = null)
    {
        using var buffered = new MemoryStream();
        await fileStream.CopyToAsync(buffered);
        var checksum = ComputeSha256(buffered.ToArray());
        buffered.Position = 0;

        var variants = await imageProcessing.ProcessAsync(buffered, "platform");
        var detail = variants.First(v => v.Suffix == "");

        var baseKey = $"platform/{Guid.NewGuid()}";
        await Task.WhenAll(variants.Select(variant =>
            fileStorage.SaveWithKeyAsync(variant.Stream, $"{baseKey}{variant.Suffix}.webp", "image/webp")));
        foreach (var v in variants) v.Stream.Dispose();

        var result = await platformImageProc.AddAsync(
            baseKey, Path.GetFileName(fileName),
            detail.SizeBytes, detail.Width, detail.Height,
            uploadedById, tag, "admin", altText, caption, "image/webp", checksum);

        Log.Information("[PlatformImage] Added {ImageId} tag={Tag} (primary={IsPrimary})",
            result.ImageId, tag, result.IsPrimary);

        return new AddPlatformImageResponse(
            result.PlatformImageId, result.ImageId, tag,
            fileStorage.GetPublicUrl($"{baseKey}.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{baseKey}_card.webp"),
            result.SortOrder, result.IsPrimary);
    }

    public async Task<List<PlatformImageDto>> ListAsync(string? tag = null)
    {
        var rows = await platformImageProc.ListAsync(tag);
        return rows.Select(r => new PlatformImageDto(
            r.PlatformImageId, r.ImageId, r.Tag,
            fileStorage.GetPublicUrl($"{r.StorageKey}.webp"),
            fileStorage.GetPublicUrl($"{r.StorageKey}_thumb.webp"),
            fileStorage.GetPublicUrl($"{r.StorageKey}_card.webp"),
            r.OriginalName, r.AltText, r.Caption, r.ContentType,
            r.SizeBytes, r.Width, r.Height,
            r.IsPrimary, r.SortOrder, r.CreatedAt)).ToList();
    }

    public async Task<bool> RemoveAsync(Guid imageId)
    {
        var rows = await platformImageProc.ListAsync();
        var target = rows.FirstOrDefault(r => r.ImageId == imageId);
        if (target is null) return false;

        var removed = await platformImageProc.RemoveAsync(imageId);
        if (!removed) return false;

        foreach (var suffix in new[] { "", "_card", "_thumb" })
            await fileStorage.DeleteAsync($"{target.StorageKey}{suffix}.webp");

        Log.Information("[PlatformImage] Removed {ImageId}", imageId);
        return true;
    }

    public Task<bool> SetPrimaryAsync(Guid imageId) => platformImageProc.SetPrimaryAsync(imageId);

    public Task ReorderAsync(List<Guid> imageIds) => platformImageProc.ReorderAsync(imageIds.ToArray());

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
