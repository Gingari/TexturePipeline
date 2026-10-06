using TexturePipeline.Api.Services.Jobs;
using Xunit;

namespace TexturePipeline.Api.Tests.Services.Jobs;

public sealed class TextureJobQueueTests
{
    [Fact]
    public async Task EnqueueAsync_WhenJobIsAdded_ShouldReturnSameJobId()
    {
        // Arrange
        var queue = new TextureJobQueue();
        var expectedJobId = Guid.NewGuid();

        using var cancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromSeconds(1));

        Guid? actualJobId = null;

        // Act
        await queue.EnqueueAsync(
            expectedJobId,
            cancellationTokenSource.Token);

        await foreach (var jobId in queue.ReadAllAsync(
                           cancellationTokenSource.Token))
        {
            actualJobId = jobId;
            break;
        }

        // Assert
        Assert.True(actualJobId.HasValue);
        Assert.Equal<Guid?>(expectedJobId, actualJobId);
    }
}