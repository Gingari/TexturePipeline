using TexturePipeline.Api.Services.Interfaces;
namespace TexturePipeline.Api.Services.Jobs;

public sealed class TextureJobProcessor : ITextureJobProcessor
{
    private readonly IJobStore _jobStore;
    private readonly IOllamaService _ollamaService;
    private readonly IImageGeneratorService _imageGenerator;
    private readonly ILogger<TextureJobProcessor> _logger;

    public TextureJobProcessor(
        IJobStore jobStore,
        IOllamaService ollamaService,
        IImageGeneratorService imageGenerator,
        ILogger<TextureJobProcessor> logger)
    {
        _jobStore = jobStore;
        _ollamaService = ollamaService;
        _imageGenerator = imageGenerator;
        _logger = logger;
    }

    public async Task ProcessAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job =
            _jobStore.GetJob(jobId);

        if (job is null)
        {
            _logger.LogWarning(
                "Job {JobId} was not found",
                jobId);

            return;
        }

        try
        {
            _jobStore.UpdateJob(
                jobId,
                "processing",
                null,
                null);

            _logger.LogInformation(
                "Job {JobId} started processing",
                jobId);

            var promptRequest =
                TexturePromptBuilder.Build(
                    job.Description,
                    job.Resolution,
                    job.Seed);

            _logger.LogInformation(
                "Job {JobId}: generating Stable Diffusion prompt",
                jobId);

            var rawPrompt =
                await _ollamaService.GenerateAsync(
                    promptRequest,
                    cancellationToken);

            var generatedPrompt =
                NormalizePrompt(rawPrompt);

            _logger.LogInformation(
                "Job {JobId}: generated prompt: {Prompt}",
                jobId,
                generatedPrompt);

            _logger.LogInformation(
                "Job {JobId}: submitting {Width}x{Height} " +
                "workflow to ComfyUI",
                jobId,
                job.Resolution,
                job.Resolution);

            var maps =
                await _imageGenerator.GenerateAsync(
                    generatedPrompt,
                    job.Resolution,
                    job.Seed,
                    cancellationToken);

            var imagePaths =
                await SaveMapsAsync(
                    jobId,
                    maps,
                    cancellationToken);

            _jobStore.UpdateJob(
                jobId,
                "done",
                generatedPrompt,
                null,
                imagePaths);

            _logger.LogInformation(
                "Job {JobId} completed successfully",
                jobId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _jobStore.UpdateJob(
                jobId,
                "failed",
                null,
                "Job was cancelled because the application is stopping.");

            _logger.LogWarning(
                "Job {JobId} was cancelled",
                jobId);

            throw;
        }
        catch (Exception exception)
        {
            _jobStore.UpdateJob(
                jobId,
                "failed",
                null,
                exception.Message);

            _logger.LogError(
                exception,
                "Job {JobId} failed",
                jobId);
        }
    }

    private static string NormalizePrompt(
        string prompt)
    {
        return prompt
            .Trim()
            .Replace(
                "\r",
                string.Empty)
            .Replace(
                "\n",
                " ");
    }

    private static async Task<Dictionary<string, string>>
        SaveMapsAsync(
            Guid jobId,
            IReadOnlyDictionary<string, byte[]> maps,
            CancellationToken cancellationToken)
    {
        var imagePaths =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var outputDirectory =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "textures",
                jobId.ToString());

        Directory.CreateDirectory(
            outputDirectory);

        foreach (var map in maps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath =
                Path.Combine(
                    outputDirectory,
                    $"{map.Key}.png");

            await File.WriteAllBytesAsync(
                filePath,
                map.Value,
                cancellationToken);

            imagePaths[map.Key] =
                filePath;
        }

        return imagePaths;
    }
}