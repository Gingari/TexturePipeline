using System.Threading.Channels;
using TexturePipeline.Api.Services.Interfaces;
namespace TexturePipeline.Api.Services.Jobs;

public sealed class TextureJobQueue : ITextureJobQueue
{
    private const int Capacity = 100;

    private readonly Channel<Guid> _channel;

    public TextureJobQueue()
    {
        var options =
            new BoundedChannelOptions(Capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            };

        _channel =
            Channel.CreateBounded<Guid>(options);
    }

    public ValueTask EnqueueAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(
            jobId,
            cancellationToken);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(
            cancellationToken);
    }
}