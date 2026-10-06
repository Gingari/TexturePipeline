using System.Collections.Concurrent;
using TexturePipeline.Api.Models;
using TexturePipeline.Api.Services.Interfaces;
namespace TexturePipeline.Api.Services;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, TextureJob> _jobs = new();

    public Guid CreateJob(string description, int resolution, int seed)
    {
        Guid _id = Guid.NewGuid();
        TextureJob newJob = new TextureJob
        {
            Id = _id,
            Description = description,
            Resolution = resolution,
            Seed = seed,
            CreatedAt = DateTimeOffset.UtcNow  
        };
        _jobs.TryAdd(_id, newJob);
        return newJob.Id;
    }

    public TextureJob? GetJob(Guid id)
    {
        _jobs.TryGetValue(id, out TextureJob? job);
        return job;
    }
    
    public void UpdateJob(Guid id, string status, string? result, string? error, Dictionary<string, string>? imagePaths = null)
    {
        _jobs.TryGetValue(id, out TextureJob? job);
        if (job == null) throw new Exception($"Job {id} not found");

        job.Status = status;
        job.Result = result;
        job.Error = error;
        if (imagePaths != null) job.ImagePaths = imagePaths;

        if (status is "done" or "failed")
            job.CompletedAt = DateTimeOffset.UtcNow;
    }
}