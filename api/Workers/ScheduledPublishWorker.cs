using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Workers;

/// <summary>
/// Checks every minute for Draft events with ScheduledPublishAt <= now,
/// transitions them to Published via stored procedure.
/// </summary>
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

                var published = await eventProc.PublishScheduledEventsAsync();
                if (published > 0)
                    Log.Information("[ScheduledPublish] Published {Count} scheduled events", published);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ScheduledPublish] Failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
