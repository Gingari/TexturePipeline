namespace TexturePipeline.Api.Services.Interfaces;

public interface ITextureJobQueue
{
    ValueTask EnqueueAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(
        CancellationToken cancellationToken = default);
}