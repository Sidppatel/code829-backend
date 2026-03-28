using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Workers;

/// <summary>
/// Checks every minute for Draft events with ScheduledPublishAt <= now,
/// transitions them to Published, and logs to admin_logs.
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
                var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
                var now = DateTime.UtcNow;

                var toPublish = await context.Events
                    .Where(e => e.Status == EventStatus.Draft
                        && e.ScheduledPublishAt != null
                        && e.ScheduledPublishAt <= now)
                    .ToListAsync(stoppingToken);

                foreach (var ev in toPublish)
                {
                    ev.Status = EventStatus.Published;
                    ev.PublishedAt = now;
                    ev.ScheduledPublishAt = null;
                    ev.UpdatedAt = now;

                    context.AdminLogs.Add(new AdminLog
                    {
                        Id = Guid.NewGuid(),
                        Action = "event.scheduled_publish",
                        EntityType = "Event",
                        EntityId = ev.Id,
                        Description = $"Event '{ev.Title}' auto-published on schedule"
                    });

                    Log.Information("[ScheduledPublish] Published event {Title} (ID: {Id})", ev.Title, ev.Id);
                }

                if (toPublish.Count > 0)
                    await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ScheduledPublish] Failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
