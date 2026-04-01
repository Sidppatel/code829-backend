using Db;
using Db.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Creates an InMemory DbContext for testing, handling NpgsqlTsVector incompatibility.
/// </summary>
public static class TestDbContextFactory
{
    public static EventPlatformDbContext Create()
    {
        ChangeTrackingInterceptor.IsSuspended = true;

        var options = new DbContextOptionsBuilder<EventPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var interceptor = new ChangeTrackingInterceptor();
        return new TestDbContext(options, interceptor);
    }
}

/// <summary>
/// Test DbContext that overrides OnModelCreating to skip PostgreSQL-specific features.
/// </summary>
public class TestDbContext : EventPlatformDbContext
{
    public TestDbContext(DbContextOptions<EventPlatformDbContext> options, ChangeTrackingInterceptor interceptor)
        : base(options, interceptor) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Remove NpgsqlTsVector properties that are incompatible with InMemory
        modelBuilder.Entity<Db.Entities.Event>(entity =>
        {
            entity.Ignore(e => e.SearchVector);
        });

        modelBuilder.Entity<Db.Entities.Views.EventView>(entity =>
        {
            entity.Ignore(e => e.SearchVector);
        });
    }
}
