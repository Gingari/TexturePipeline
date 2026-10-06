namespace TexturePipeline.Api.Services.Interfaces;

public interface ITextureJobProcessor
{
    Task ProcessAsync(
        Guid jobId,
        CancellationToken cancellationToken);
}