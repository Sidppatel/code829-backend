using System.Threading.Channels;
using Contracts.Enums;

namespace Api.Services;

public class LogQueueEntry
{
    public required string EventType { get; set; }
    public required AuditActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? SubjectType { get; set; }
    public Guid? SubjectId { get; set; }
    public required string Action { get; set; }
    public string? MetadataJson { get; set; }
    public string? Ip { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface IDbLoggingService
{
    bool Enqueue(LogQueueEntry entry);
    ChannelReader<LogQueueEntry> Reader { get; }
}

public class DbLoggingService : IDbLoggingService
{
    private readonly Channel<LogQueueEntry> _channel;

    public DbLoggingService()
    {
        // Unbounded channel is appropriate since we are only logging warnings/errors & visits,
        // which has very low volume compared to request counts.
        _channel = Channel.CreateUnbounded<LogQueueEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool Enqueue(LogQueueEntry entry)
    {
        return _channel.Writer.TryWrite(entry);
    }

    public ChannelReader<LogQueueEntry> Reader => _channel.Reader;
}
