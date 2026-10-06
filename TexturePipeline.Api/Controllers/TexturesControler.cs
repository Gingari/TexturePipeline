using Microsoft.AspNetCore.Mvc;
using TexturePipeline.Api.Models;
using TexturePipeline.Api.Services.Interfaces;

namespace TexturePipeline.Api.Controllers;

[ApiController]
[Route("api/textures")]
public sealed class TexturesController : ControllerBase
{
    private readonly IJobStore _jobStore;
    private readonly ITextureJobQueue _queue;
    private readonly ILogger<TexturesController> _logger;

    public TexturesController(
        IJobStore jobStore,
        ITextureJobQueue queue,
        ILogger<TexturesController> logger)
    {
        _jobStore = jobStore;
        _queue = queue;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateTextureRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Resolution % 64 != 0)
        {
            ModelState.AddModelError(
                nameof(request.Resolution),
                "Resolution must be divisible by 64.");

            return ValidationProblem(ModelState);
        }

        var seed =
            request.Seed
            ?? Random.Shared.Next(1, int.MaxValue);

        var jobId =
            _jobStore.CreateJob(
                request.Description,
                request.Resolution,
                seed);

        await _queue.EnqueueAsync(
            jobId,
            cancellationToken);

        _logger.LogInformation(
            "Job {JobId} was added to the generation queue. " +
            "Resolution: {Width}x{Height}, Seed: {Seed}",
            jobId,
            request.Resolution,
            request.Resolution,
            seed);

        return Accepted(new
        {
            jobId,
            status = "pending",
            seed,
            resolution = request.Resolution,
            statusUrl = Url.ActionLink(
                nameof(GetJob),
                values: new { id = jobId })
        });
    }

    [HttpGet("jobs/{id:guid}")]
    public IActionResult GetJob(Guid id)
    {
        var job =
            _jobStore.GetJob(id);

        if (job is null)
        {
            return NotFound(new
            {
                error = "Job not found"
            });
        }

        return Ok(job);
    }

    [HttpGet("jobs/{id:guid}/image/{mapName}")]
    public IActionResult GetImage(
        Guid id,
        string mapName)
    {
        var job =
            _jobStore.GetJob(id);

        if (job is null)
        {
            return NotFound(new
            {
                error = "Job not found"
            });
        }

        if (job.Status != "done")
        {
            return Conflict(new
            {
                error = "Job is not completed",
                status = job.Status
            });
        }

        if (!job.ImagePaths.TryGetValue(
                mapName,
                out var path))
        {
            return NotFound(new
            {
                error = $"Map '{mapName}' was not found"
            });
        }

        if (!System.IO.File.Exists(path))
        {
            return NotFound(new
            {
                error = "Image file was not found on disk"
            });
        }

        return PhysicalFile(
            path,
            "image/png",
            $"{id}_{mapName}.png");
    }
}