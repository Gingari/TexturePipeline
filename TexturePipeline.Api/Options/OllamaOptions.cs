namespace TexturePipeline.Api.Options
{
public class OllamaOption
{
    public string BaseUrl { get; init; } = string.Empty;
    public string Model { get; init; } = "phi3:mini";
    public int TimeoutMinutes { get; init; } = 2;
}
}