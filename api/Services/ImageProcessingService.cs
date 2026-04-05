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
        var results = new List<ProcessedImage>();

        foreach (var variant in variants)
        {
            var clone = image.Clone(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(variant.MaxWidth, variant.MaxHeight),
                    Mode = entityType == "user" ? ResizeMode.Crop : ResizeMode.Max
                });
            });

            var ms = new MemoryStream();
            await clone.SaveAsWebpAsync(ms, new WebpEncoder { Quality = 80 });
            ms.Position = 0;

            results.Add(new ProcessedImage(
                ms,
                variant.Suffix,
                clone.Width,
                clone.Height,
                (int)ms.Length
            ));

            clone.Dispose();
        }

        return results;
    }

    public async Task<(int Width, int Height)> GetDimensionsAsync(Stream input)
    {
        input.Position = 0;
        var info = await Image.IdentifyAsync(input);
        return (info.Width, info.Height);
    }
}
