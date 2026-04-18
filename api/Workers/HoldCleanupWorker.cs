using Api.Services;
using Serilog;

namespace Api.Workers;

public class HoldCleanupWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tableBookingService = scope.ServiceProvider.GetRequiredService<ITableBookingService>();

                var tablesCleaned = await tableBookingService.CleanupExpiredLocksAsync();
                if (tablesCleaned > 0)
                    Log.Information("[HoldCleanup] Cleaned {Count} expired table locks/purchases", tablesCleaned);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HoldCleanup] Failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
