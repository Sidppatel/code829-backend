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
            new ImageVariant("", 1200, 800),
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
        ["business_user"] =
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        input.Position = 0;
        using var image = await Image.LoadAsync(input);
        var loaded = sw.ElapsedMilliseconds;

        var variants = VariantsByEntity.GetValueOrDefault(entityType, VariantsByEntity["event"]);
        var cropEntities = entityType is "user" or "business_user";
        var resizeMode = cropEntities ? ResizeMode.Crop : ResizeMode.Max;

        var encoder = new WebpEncoder
        {
            Quality = 75,
            FileFormat = WebpFileFormatType.Lossy,
            Method = WebpEncodingMethod.Fastest
        };

        var tasks = variants.Select(async variant =>
        {

            using var clone = (image.Width <= variant.MaxWidth && image.Height <= variant.MaxHeight && resizeMode == ResizeMode.Max)
                ? image.Clone(_ => { })
                : image.Clone(ctx =>
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(variant.MaxWidth, variant.MaxHeight),
                        Mode = resizeMode
                    }));

            var width = clone.Width;
            var height = clone.Height;
            var ms = new MemoryStream();
            await clone.SaveAsWebpAsync(ms, encoder);
            ms.Position = 0;

            return new ProcessedImage(ms, variant.Suffix, width, height, (int)ms.Length);
        });

        var result = await Task.WhenAll(tasks);
        Serilog.Log.Information("[ImgProc] entity={Entity} variants={Count} src={SrcW}x{SrcH} timing load={Load}ms encode={Encode}ms total={Total}ms",
            entityType, result.Length, image.Width, image.Height, loaded, sw.ElapsedMilliseconds - loaded, sw.ElapsedMilliseconds);
        return [.. result];
    }

    public async Task<(int Width, int Height)> GetDimensionsAsync(Stream input)
    {
        input.Position = 0;
        var info = await Image.IdentifyAsync(input);
        return (info.Width, info.Height);
    }
}
