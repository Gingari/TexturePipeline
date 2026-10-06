using System.ComponentModel.DataAnnotations;

namespace TexturePipeline.Api.Models;

public sealed record GenerateTextureRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public required string Description { get; init; }

    [Range(512, 2048)]
    public int Resolution { get; init; } = 1024;

    [Range(1, int.MaxValue)]
    public int? Seed { get; init; }
}