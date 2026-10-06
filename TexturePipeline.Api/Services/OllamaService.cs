using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TexturePipeline.Api.Models;
using TexturePipeline.Api.Options;
namespace TexturePipeline.Api.Services
{

    public interface IOllamaService
    {
        Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
    }
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model; 

        public OllamaService(HttpClient httpClient, IOptions<OllamaOption> options)
        {
            _httpClient = httpClient;
            _model = options.Value.Model;
        }
        public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
        {
            var request = new OllamaApiRequest(
                Model: _model,
                Messages: [new OllamaChatMessage("user",prompt)],
                Stream: false
            );
            var response = await _httpClient.PostAsJsonAsync("/api/chat",request,ct);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<OllamaApiResponse>(ct);
            return result?.Message.Content
                ?? throw new InvalidOperationException("Ollama returned empty response");
        }

    }
}