namespace Api.Services;

public record ImageVariant(string Suffix, int MaxWidth, int MaxHeight);

public record ProcessedImage(
    Stream Stream,
    string Suffix,
    int Width,
    int Height,
    int SizeBytes
);

public interface IImageProcessingService
{
    Task<List<ProcessedImage>> ProcessAsync(Stream input, string entityType);
    Task<(int Width, int Height)> GetDimensionsAsync(Stream input);
}
