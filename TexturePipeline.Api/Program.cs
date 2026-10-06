using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TexturePipeline.Api.Health;
using TexturePipeline.Api.Services.Interfaces;
using TexturePipeline.Api.Options;
using TexturePipeline.Api.Services.Jobs;
using TexturePipeline.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<OllamaOption>(
    builder.Configuration.GetSection("Ollama"));

builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();

builder.Services.AddSingleton<
    ITextureJobQueue,
    TextureJobQueue>();

builder.Services.AddScoped<
    ITextureJobProcessor,
    TextureJobProcessor>();

builder.Services.AddHostedService<
    TextureJobWorker>();

builder.Services.AddHttpClient<IOllamaService, OllamaService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<OllamaOption>>()
            .Value;

        client.BaseAddress =
            new Uri(options.BaseUrl);

        client.Timeout =
            TimeSpan.FromMinutes(
                options.TimeoutMinutes);
    });

builder.Services.AddHttpClient(
    "OllamaHealth",
    client =>
    {
        var baseUrl =
            builder.Configuration["Ollama:BaseUrl"]
            ?? "http://ollama:11434";

        client.BaseAddress =
            new Uri(baseUrl);

        client.Timeout =
            TimeSpan.FromSeconds(10);
    });

builder.Services.AddHttpClient(
    "ComfyUI",
    client =>
    {
        var baseUrl =
            builder.Configuration["Services:ComfyUiUrl"]
            ?? "http://comfyui:8188";

        client.BaseAddress =
            new Uri(baseUrl);

        client.Timeout =
            TimeSpan.FromMinutes(15);
    });

builder.Services.AddHttpClient(
    "PythonBridge",
    client =>
    {
        var baseUrl =
            builder.Configuration["Services:PythonBridgeUrl"]
            ?? "http://python-engine:5001";

        client.BaseAddress =
            new Uri(baseUrl);

        client.Timeout =
            TimeSpan.FromMinutes(2);
    });

builder.Services.AddScoped<
    IImageGeneratorService,
    DiffusersService>();

builder.Services
    .AddHealthChecks()
    .AddCheck<DependenciesHealthCheck>(
        "dependencies",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(15));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check =>
            check.Tags.Contains("ready")
    });

app.MapControllers();

app.Run();