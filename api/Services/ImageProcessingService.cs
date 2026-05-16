using SkiaSharp;

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

        using var skData = SKData.Create(input);
        using var codec = SKCodec.Create(skData);
        using var bitmap = SKBitmap.Decode(codec);

        var loaded = sw.ElapsedMilliseconds;

        var variants = VariantsByEntity.GetValueOrDefault(entityType, VariantsByEntity["event"]);
        var cropEntities = entityType is "user" or "business_user";

        var tasks = variants.Select(variant =>
        {
            SKBitmap resizedBitmap;

            if (bitmap.Width <= variant.MaxWidth && bitmap.Height <= variant.MaxHeight && !cropEntities)
            {
                resizedBitmap = bitmap.Copy();
            }
            else
            {
                var resizeInfo = CalculateResizeDimensions(bitmap.Width, bitmap.Height, variant.MaxWidth, variant.MaxHeight, cropEntities);
                resizedBitmap = bitmap.Resize(resizeInfo, new SKSamplingOptions(SKFilterMode.Linear));
            }

            var width = resizedBitmap.Width;
            var height = resizedBitmap.Height;

            var ms = new MemoryStream();
            resizedBitmap.Encode(ms, SKEncodedImageFormat.Webp, 75);
            ms.Position = 0;

            resizedBitmap.Dispose();

            return Task.FromResult(new ProcessedImage(ms, variant.Suffix, width, height, (int)ms.Length));
        });

        var result = await Task.WhenAll(tasks);
        Serilog.Log.Information("[ImgProc] entity={Entity} variants={Count} src={SrcW}x{SrcH} timing load={Load}ms encode={Encode}ms total={Total}ms",
            entityType, result.Length, bitmap.Width, bitmap.Height, loaded, sw.ElapsedMilliseconds - loaded, sw.ElapsedMilliseconds);
        return [.. result];
    }

    public Task<(int Width, int Height)> GetDimensionsAsync(Stream input)
    {
        input.Position = 0;
        using var skData = SKData.Create(input);
        using var codec = SKCodec.Create(skData);
        return Task.FromResult((codec.Info.Width, codec.Info.Height));
    }

    private SKImageInfo CalculateResizeDimensions(int srcWidth, int srcHeight, int maxWidth, int maxHeight, bool isCrop)
    {
        float ratioX = (float)maxWidth / srcWidth;
        float ratioY = (float)maxHeight / srcHeight;
        float ratio = isCrop ? Math.Max(ratioX, ratioY) : Math.Min(ratioX, ratioY);

        int newWidth = (int)(srcWidth * ratio);
        int newHeight = (int)(srcHeight * ratio);

        return new SKImageInfo(newWidth, newHeight);
    }
}
