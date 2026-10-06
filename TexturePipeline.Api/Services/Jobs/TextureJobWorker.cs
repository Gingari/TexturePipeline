using TexturePipeline.Api.Services.Interfaces;

namespace TexturePipeline.Api.Services.Jobs;

public sealed class TextureJobWorker : BackgroundService
{
    private readonly ITextureJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TextureJobWorker> _logger;

    public TextureJobWorker(
        ITextureJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TextureJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Texture job worker started");

        try
        {
            await foreach (
                var jobId in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessJobAsync(
                    jobId,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Texture job worker is stopping");
        }
    }

    private async Task ProcessJobAsync(
        Guid jobId,
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var processor =
                scope.ServiceProvider
                    .GetRequiredService<ITextureJobProcessor>();

            await processor.ProcessAsync(
                jobId,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected worker error while processing job {JobId}",
                jobId);
        }
    }
}