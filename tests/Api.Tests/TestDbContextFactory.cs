using Db;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public static class TestDbContextFactory
{
    public static EventPlatformDbContext Create()
    {
        var options = new DbContextOptionsBuilder<EventPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestDbContext(options);
    }
}

public class TestDbContext : EventPlatformDbContext
{
    public TestDbContext(DbContextOptions<EventPlatformDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Db.Entities.Event>(entity =>
        {
            entity.Ignore(e => e.SearchVector);
        });
    }
}
