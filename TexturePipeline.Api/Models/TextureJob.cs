using TexturePipeline.Api.Models;
public class TextureJob
{
    public Guid Id { get; init; }
    public string Status { get; set; } = "pending";
    public string Description { get; init; } = string.Empty;
    public int Resolution { get; init; }
    public int Seed { get; init; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string> ImagePaths { get; set; } = new(); 
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum TextureJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}