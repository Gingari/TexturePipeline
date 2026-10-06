namespace TexturePipeline.Api.Services.Interfaces;

public interface IImageGeneratorService
{
    Task<Dictionary<string, byte[]>> GenerateAsync(
        string prompt,
        int resolution,
        int seed,
        CancellationToken ct = default);
}