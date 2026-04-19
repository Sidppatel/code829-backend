using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Api.Services;

public class ImageProcessingService : IImageProcessingService
{
    private static readonly Dictionary<string, List<ImageVariant>> VariantsByEntity = new()
    {
        ["venue"] =
        [
            new ImageVariant("", 1200, 800),        // detail (original key)
            new ImageVariant("_card", 400, 300),
            new ImageVariant("_thumb", 150, 150)
        ],
        ["event"] =
        [
            new ImageVariant("", 1200, 800),
            new ImageVariant("_card", 400, 300),
            new ImageVariant("_thumb", 150, 150)
        ],
        ["user"] =
        [
            new ImageVariant("", 200, 200),
            new ImageVariant("_thumb", 80, 80)
        ],
        ["platform"] =
        [
            new ImageVariant("", 400, 400),
            new ImageVariant("_thumb", 80, 80)
        ]
    };

    public async Task<List<ProcessedImage>> ProcessAsync(Stream input, string entityType)
    {
        input.Position = 0;
        using var image = await Image.LoadAsync(input);

        var variants = VariantsByEntity.GetValueOrDefault(entityType, VariantsByEntity["event"]);
        var resizeMode = entityType == "user" ? ResizeMode.Crop : ResizeMode.Max;

        // Process all variants in parallel — each clone is independent so this is safe.
        var tasks = variants.Select(async variant =>
        {
            using var clone = image.Clone(ctx =>
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(variant.MaxWidth, variant.MaxHeight),
                    Mode = resizeMode
                }));

            var width = clone.Width;
            var height = clone.Height;
            var ms = new MemoryStream();
            await clone.SaveAsWebpAsync(ms, new WebpEncoder { Quality = 80 });
            ms.Position = 0;

            return new ProcessedImage(ms, variant.Suffix, width, height, (int)ms.Length);
        });

        return [.. await Task.WhenAll(tasks)];
    }

    public async Task<(int Width, int Height)> GetDimensionsAsync(Stream input)
    {
        input.Position = 0;
        var info = await Image.IdentifyAsync(input);
        return (info.Width, info.Height);
    }
}
