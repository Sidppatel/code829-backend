using Api.Services;
using Db.Repositories;
using Serilog;

namespace Api.Workers;

/// <summary>
/// Background worker that runs daily to clean up old logs based on configurable retention periods.
/// Default retention: developer 90 days, admin 365 days, system 30 days.
/// </summary>
public class LogCleanupWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var logRepo = scope.ServiceProvider.GetRequiredService<ILogRepository>();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

                var devRetention = int.Parse(await settings.GetOrDefaultAsync("dev_log_retention_days", "90") ?? "90");
                var adminRetention = int.Parse(await settings.GetOrDefaultAsync("admin_log_retention_days", "365") ?? "365");
                var systemRetention = int.Parse(await settings.GetOrDefaultAsync("system_log_retention_days", "30") ?? "30");

                var devCleaned = await logRepo.CleanupDeveloperLogsAsync(devRetention);
                var adminCleaned = await logRepo.CleanupAdminLogsAsync(adminRetention);
                var systemCleaned = await logRepo.CleanupSystemLogsAsync(systemRetention);

                if (devCleaned + adminCleaned + systemCleaned > 0)
                {
                    Log.Information(
                        "[LogCleanup] Cleaned dev={Dev}, admin={Admin}, system={System}",
                        devCleaned, adminCleaned, systemCleaned);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[LogCleanup] Failed");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
