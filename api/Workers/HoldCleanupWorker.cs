using Api.Services;
using Serilog;

namespace Api.Workers;

/// <summary>
/// Background worker that cleans up expired seat holds every 30 seconds.
/// Uses ISeatService.CleanupExpiredHoldsAsync() to deactivate stale holds.
/// </summary>
public class HoldCleanupWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var seatService = scope.ServiceProvider.GetRequiredService<ISeatService>();
                var tableBookingService = scope.ServiceProvider.GetRequiredService<ITableBookingService>();

                var seatsCleaned = await seatService.CleanupExpiredHoldsAsync();
                if (seatsCleaned > 0)
                    Log.Information("[HoldCleanup] Expired {Count} seat holds", seatsCleaned);

                var tablesCleaned = await tableBookingService.CleanupExpiredLocksAsync();
                if (tablesCleaned > 0)
                    Log.Information("[HoldCleanup] Cleaned {Count} expired table locks/bookings", tablesCleaned);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HoldCleanup] Failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
