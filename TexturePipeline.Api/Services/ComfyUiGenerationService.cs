using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using TexturePipeline.Api.Services.Interfaces;
namespace TexturePipeline.Api.Services;

public sealed class DiffusersService : IImageGeneratorService
{
    private static readonly TimeSpan GenerationTimeout =
        TimeSpan.FromMinutes(15);

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(2);

    private readonly HttpClient _comfyUiClient;
    private readonly HttpClient _pythonClient;
    private readonly ILogger<DiffusersService> _logger;
    private readonly string _workflowJsonTemplate;

    public DiffusersService(
        IHttpClientFactory httpClientFactory,
        ILogger<DiffusersService> logger)
    {
        _comfyUiClient =
            httpClientFactory.CreateClient("ComfyUI");

        _pythonClient =
            httpClientFactory.CreateClient("PythonBridge");

        _logger = logger;

        var workflowPath = Path.Combine(
            AppContext.BaseDirectory,
            "workflow_api.json");

        if (!File.Exists(workflowPath))
        {
            throw new FileNotFoundException(
                $"Workflow file was not found: {workflowPath}");
        }

        _workflowJsonTemplate =
            File.ReadAllText(workflowPath);
    }

    public async Task<Dictionary<string, byte[]>> GenerateAsync(
        string prompt,
        int resolution,
        int seed,
        CancellationToken ct = default)
    {
        ValidateResolution(resolution);

        var workflowNodes =
            JsonNode.Parse(_workflowJsonTemplate)?.AsObject()
            ?? throw new InvalidOperationException(
                "Failed to parse workflow_api.json.");

        SetPrompt(
            workflowNodes,
            prompt);

        SetResolution(
            workflowNodes,
            resolution);

        var finalSeed = SetSeed(
            workflowNodes,
            seed);

        SetOutputPrefix(
            workflowNodes);

        _logger.LogInformation(
            "Submitting workflow to ComfyUI. " +
            "Resolution: {Width}x{Height}, Seed: {Seed}",
            resolution,
            resolution,
            finalSeed);

        var promptId =
            await SubmitWorkflowAsync(
                workflowNodes,
                ct);

        _logger.LogInformation(
            "ComfyUI accepted prompt {PromptId}",
            promptId);

        var imageName =
            await PollForImageFilenameAsync(
                promptId,
                ct);

        var albedoBytes =
            await DownloadImageAsync(
                imageName,
                ct);

        var pbrMaps =
            await ProcessPbrMapsWithPythonAsync(
                albedoBytes,
                ct);

        pbrMaps["albedo"] = albedoBytes;

        return pbrMaps;
    }

    private static void ValidateResolution(
        int resolution)
    {
        if (resolution is < 512 or > 2048)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution),
                "Resolution must be between 512 and 2048.");
        }

        if (resolution % 64 != 0)
        {
            throw new ArgumentException(
                "Resolution must be divisible by 64.",
                nameof(resolution));
        }
    }

    private static void SetPrompt(
        JsonObject workflowNodes,
        string prompt)
    {
        var positivePromptInputs =
            workflowNodes["5"]?["inputs"]
            ?? throw new InvalidOperationException(
                "Positive prompt node with ID 5 was not found.");

        positivePromptInputs["text"] = prompt;
    }

    private static void SetResolution(
        JsonObject workflowNodes,
        int resolution)
    {
        var latentImageInputs =
            workflowNodes["6"]?["inputs"]
            ?? throw new InvalidOperationException(
                "EmptyLatentImage node with ID 6 was not found.");

        latentImageInputs["width"] = resolution;
        latentImageInputs["height"] = resolution;
        latentImageInputs["batch_size"] = 1;
    }

    private static int SetSeed(
        JsonObject workflowNodes,
        int seed)
    {
        var samplerInputs =
            workflowNodes["7"]?["inputs"]
            ?? throw new InvalidOperationException(
                "KSampler node with ID 7 was not found.");

        var finalSeed = seed > 0
            ? seed
            : Random.Shared.Next(1, int.MaxValue);

        samplerInputs["seed"] = finalSeed;

        return finalSeed;
    }

    private static void SetOutputPrefix(
        JsonObject workflowNodes)
    {
        var saveImageInputs =
            workflowNodes["9"]?["inputs"];

        if (saveImageInputs is null)
        {
            return;
        }

        var identifier =
            Guid.NewGuid().ToString("N")[..8];

        saveImageInputs["filename_prefix"] =
            $"PBR_{identifier}";
    }

    private async Task<string> SubmitWorkflowAsync(
        JsonObject workflowNodes,
        CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["prompt"] = workflowNodes
        };

        using var response =
            await _comfyUiClient.PostAsJsonAsync(
                "/prompt",
                payload,
                ct);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(ct);

            throw new HttpRequestException(
                $"ComfyUI returned HTTP " +
                $"{(int)response.StatusCode}: {responseBody}");
        }

        var responseJson =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);

        if (!responseJson.TryGetProperty(
                "prompt_id",
                out var promptIdElement))
        {
            throw new InvalidOperationException(
                "ComfyUI response does not contain prompt_id.");
        }

        var promptId =
            promptIdElement.GetString();

        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new InvalidOperationException(
                "ComfyUI returned an empty prompt_id.");
        }

        return promptId;
    }

    private async Task<string> PollForImageFilenameAsync(
        string promptId,
        CancellationToken ct)
    {
        var deadline =
            DateTimeOffset.UtcNow + GenerationTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            await Task.Delay(
                PollInterval,
                ct);

            using var response =
                await _comfyUiClient.GetAsync(
                    $"/history/{Uri.EscapeDataString(promptId)}",
                    ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ComfyUI history request for {PromptId} " +
                    "returned HTTP {StatusCode}",
                    promptId,
                    response.StatusCode);

                continue;
            }

            var historyJson =
                await response.Content
                    .ReadFromJsonAsync<JsonElement>(
                        cancellationToken: ct);

            if (!historyJson.TryGetProperty(
                    promptId,
                    out var taskData))
            {
                continue;
            }

            ThrowIfWorkflowFailed(
                promptId,
                taskData);

            if (!taskData.TryGetProperty(
                    "outputs",
                    out var outputs))
            {
                continue;
            }

            foreach (var outputNode in outputs.EnumerateObject())
            {
                if (!outputNode.Value.TryGetProperty(
                        "images",
                        out var images)
                    || images.ValueKind != JsonValueKind.Array
                    || images.GetArrayLength() == 0)
                {
                    continue;
                }

                var firstImage =
                    images[0];

                if (!firstImage.TryGetProperty(
                        "filename",
                        out var filenameElement))
                {
                    continue;
                }

                var filename =
                    filenameElement.GetString();

                if (string.IsNullOrWhiteSpace(filename))
                {
                    continue;
                }

                _logger.LogInformation(
                    "ComfyUI completed prompt {PromptId}",
                    promptId);

                return filename;
            }
        }

        throw new TimeoutException(
            $"ComfyUI did not complete generation within " +
            $"{GenerationTimeout.TotalMinutes:0} minutes.");
    }

    private static void ThrowIfWorkflowFailed(
        string promptId,
        JsonElement taskData)
    {
        if (!taskData.TryGetProperty(
                "status",
                out var status))
        {
            return;
        }

        if (!status.TryGetProperty(
                "status_str",
                out var statusString))
        {
            return;
        }

        if (statusString.GetString() != "error")
        {
            return;
        }

        throw new InvalidOperationException(
            $"ComfyUI workflow {promptId} failed: " +
            taskData.GetRawText());
    }

    private async Task<byte[]> DownloadImageAsync(
        string imageName,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Downloading generated image {ImageName}",
            imageName);

        var encodedImageName =
            Uri.EscapeDataString(imageName);

        return await _comfyUiClient.GetByteArrayAsync(
            $"/view?filename={encodedImageName}&type=output",
            ct);
    }

    private async Task<Dictionary<string, byte[]>>
        ProcessPbrMapsWithPythonAsync(
            byte[] albedoBytes,
            CancellationToken ct)
    {
        using var content =
            new MultipartFormDataContent();

        using var imageContent =
            new ByteArrayContent(albedoBytes);

        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        content.Add(
            imageContent,
            "file",
            "albedo.png");

        _logger.LogInformation(
            "Submitting albedo image to Python PBR Engine");

        using var response =
            await _pythonClient.PostAsync(
                "/generate-pbr",
                content,
                ct);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(ct);

            throw new HttpRequestException(
                $"Python PBR Engine returned HTTP " +
                $"{(int)response.StatusCode}: {responseBody}");
        }

        var base64Maps =
            await response.Content
                .ReadFromJsonAsync<Dictionary<string, string>>(
                    cancellationToken: ct)
            ?? throw new InvalidOperationException(
                "Python PBR Engine returned an empty response.");

        var result =
            new Dictionary<string, byte[]>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var map in base64Maps)
        {
            result[map.Key] =
                Convert.FromBase64String(map.Value);
        }

        _logger.LogInformation(
            "Python PBR Engine generated {MapCount} maps",
            result.Count);

        return result;
    }
}