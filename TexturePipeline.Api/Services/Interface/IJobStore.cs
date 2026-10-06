using TexturePipeline.Api.Models;
namespace TexturePipeline.Api.Services.Interfaces;

public interface IJobStore
{
    Guid CreateJob(string preset, int resolution, int seed);
    TextureJob? GetJob(Guid id);
    void UpdateJob(Guid id, string status, string? result, string? error, Dictionary<string, string>? imagePaths = null);
}