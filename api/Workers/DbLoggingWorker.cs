using Api.Services;
using Db.Entities;
using Serilog;

namespace Api.Workers;

public class DbLoggingWorker(
    IDbLoggingService loggingService,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("[DbLoggingWorker] Started background database logging worker");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await loggingService.Reader.WaitToReadAsync(stoppingToken))
                {
                    while (loggingService.Reader.TryRead(out var entry))
                    {
                        await WriteLogToDbWithRetryAsync(entry);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                // Log to Console only, avoid enqueuing to prevent loop
                Console.WriteLine($"[DbLoggingWorker] Unhandled loop exception: {ex}");
            }
        }
    }

    private async Task WriteLogToDbWithRetryAsync(LogQueueEntry entry)
    {
        const int maxRetries = 3;
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(2);

        while (attempt < maxRetries)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                await auditLogService.LogAsync(
                    eventType: entry.EventType,
                    actorType: entry.ActorType,
                    actorId: entry.ActorId,
                    subjectType: entry.SubjectType,
                    subjectId: entry.SubjectId,
                    action: entry.Action,
                    metadataJson: entry.MetadataJson,
                    ip: entry.Ip,
                    correlationId: entry.CorrelationId
                );

                return; // Succeeded, exit retry loop
            }
            catch (Exception ex)
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    Console.WriteLine($"[DbLoggingWorker] Failed to write log to DB after {maxRetries} attempts: {ex.Message}");
                }
                else
                {
                    await Task.Delay(delay);
                    delay *= 2; // Exponential backoff
                }
            }
        }
    }
}
