namespace TexturePipeline.Api.Models
{
    public record OllamaRequest(string Prompt);
    public record OllamaChatMessage(string Role, string Content);
    public record OllamaApiRequest(string Model, List<OllamaChatMessage> Messages,bool Stream = false);
    public record OllamaApiResponseMessage(string Role, string Content);
    public record OllamaApiResponse(string Model, OllamaApiResponseMessage Message,bool Done);
    
}