using Api.Services;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Workers;

public class ScheduledPublishWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var eventProc = scope.ServiceProvider.GetRequiredService<IEventProcedures>();
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

                var publishedIds = await eventProc.PublishScheduledEventsAsync();
                if (publishedIds.Count > 0)
                {
                    Log.Information("[ScheduledPublish] Published {Count} scheduled events", publishedIds.Count);
                    foreach (var id in publishedIds)
                        await cache.InvalidateEventAsync(id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ScheduledPublish] Failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
