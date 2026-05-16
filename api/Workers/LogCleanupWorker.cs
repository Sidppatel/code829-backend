using Api.Services;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Workers;

public class LogCleanupWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var logProc = scope.ServiceProvider.GetRequiredService<ILogProcedures>();
                var authProc = scope.ServiceProvider.GetRequiredService<IAuthProcedures>();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

                var devRetention = int.Parse(await settings.GetOrDefaultAsync("dev_log_retention_days", "90") ?? "90");
                var adminRetention = int.Parse(await settings.GetOrDefaultAsync("admin_log_retention_days", "365") ?? "365");
                var systemRetention = int.Parse(await settings.GetOrDefaultAsync("system_log_retention_days", "30") ?? "30");

                var logsCleaned = await logProc.CleanupOldLogsAsync(devRetention, adminRetention, systemRetention);
                var sessionsCleaned = await authProc.CleanupExpiredSessionsAsync();

                if (logsCleaned + sessionsCleaned > 0)
                {
                    Log.Information(
                        "[LogCleanup] Cleaned {LogCount} old logs, {SessionCount} expired sessions",
                        logsCleaned, sessionsCleaned);
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
