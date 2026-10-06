using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TexturePipeline.Api.Health;

public sealed class DependenciesHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DependenciesHealthCheck> _logger;

    public DependenciesHealthCheck(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DependenciesHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        try
        {
            await CheckComfyUiAsync(
                data,
                cancellationToken);

            await CheckPythonEngineAsync(
                data,
                cancellationToken);

            await CheckOllamaAsync(
                data,
                cancellationToken);

            await CheckStorageAsync(
                data,
                cancellationToken);

            return HealthCheckResult.Healthy(
                "All dependencies are available.",
                data);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Dependency health check failed");

            return HealthCheckResult.Unhealthy(
                "One or more dependencies are unavailable.",
                exception,
                data);
        }
    }

    private async Task CheckComfyUiAsync(
        IDictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        var client =
            _httpClientFactory.CreateClient("ComfyUI");

        using var response =
            await client.GetAsync(
                "/system_stats",
                cancellationToken);

        response.EnsureSuccessStatusCode();

        data["comfyui"] = "healthy";
    }

    private async Task CheckPythonEngineAsync(
        IDictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        var client =
            _httpClientFactory.CreateClient("PythonBridge");

        using var response =
            await client.GetAsync(
                "/docs",
                cancellationToken);

        response.EnsureSuccessStatusCode();

        data["pythonEngine"] = "healthy";
    }

    private async Task CheckOllamaAsync(
        IDictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        var client =
            _httpClientFactory.CreateClient("OllamaHealth");

        using var response =
            await client.GetAsync(
                "/api/tags",
                cancellationToken);

        response.EnsureSuccessStatusCode();

        data["ollama"] = "healthy";
    }

    private Task CheckStorageAsync(
        IDictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storagePath =
            _configuration["Storage:RootPath"]
            ?? Path.Combine(
                Directory.GetCurrentDirectory(),
                "textures");

        Directory.CreateDirectory(storagePath);

        var testFilePath =
            Path.Combine(
                storagePath,
                $".health-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(
                testFilePath,
                "health-check");

            data["storage"] = "healthy";
            data["storagePath"] = storagePath;
        }
        finally
        {
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }

        return Task.CompletedTask;
    }
}