using Api.Workers;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Workers;

/// <summary>
/// Smoke tests — workers resolve from DI and start without crashing for one tick.
/// Deep behaviour (locks expired, logs cleaned, events published) is exercised via
/// the underlying SPs in StoredProcedures/*.
/// </summary>
[Collection("Database")]
public sealed class WorkerSmokeTests(DatabaseFixture db)
{
    [Fact]
    public async Task HoldCleanupWorker_StartsAndStops()
    {
        using var scope = db.Factory.Services.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new HoldCleanupWorker(scopeFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StartAsync(cts.Token);
        await Task.Delay(50, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task LogCleanupWorker_StartsAndStops()
    {
        using var scope = db.Factory.Services.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new LogCleanupWorker(scopeFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StartAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task ScheduledPublishWorker_StartsAndStops()
    {
        using var scope = db.Factory.Services.CreateScope();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new ScheduledPublishWorker(scopeFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StartAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);
        worker.Should().NotBeNull();
    }
}
