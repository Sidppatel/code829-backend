using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpCancelPurchaseTests(DatabaseFixture db)
{
    private async Task<(Guid purchaseId, Guid tableId)> SeedPurchaseWithTableAsync()
    {
        var userId = await TestSeed.SeedUserAsync(db);
        var eventId = await TestSeed.SeedEventAsync(db, new TestSeed.EventOptions(LayoutMode: "Grid", MaxCapacity: 200));
        var eventTableId = await TestSeed.SeedEventTableAsync(db, eventId);
        var tableId = await TestSeed.SeedTableAsync(db, eventId, eventTableId, status: "Booked");
        var purchaseId = await TestSeed.SeedPurchaseAsync(db, userId, eventId);

        await TestSeed.SeedPurchaseTableAsync(db, purchaseId, tableId);

        return (purchaseId, tableId);
    }

    [Fact]
    public async Task Cancel_SetsPurchaseStatusToCancelled()
    {
        var (purchaseId, _) = await SeedPurchaseWithTableAsync();

        await db.ExecuteSqlAsync("SELECT sp_cancel_purchase(@pid)", ("pid", purchaseId));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Status\" FROM purchases WHERE \"Id\" = @pid";
        cmd.Parameters.AddWithValue("pid", purchaseId);

        var status = (string?)await cmd.ExecuteScalarAsync();
        status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Cancel_ReleasesBookedTable()
    {
        var (purchaseId, tableId) = await SeedPurchaseWithTableAsync();

        await db.ExecuteSqlAsync("SELECT sp_cancel_purchase(@pid)", ("pid", purchaseId));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Status\" FROM tables WHERE \"Id\" = @tid";
        cmd.Parameters.AddWithValue("tid", tableId);

        var tableStatus = (string?)await cmd.ExecuteScalarAsync();
        tableStatus.Should().Be("Available");
    }

    [Fact]
    public async Task Cancel_IsIdempotent_WhenCalledTwice()
    {
        var (purchaseId, _) = await SeedPurchaseWithTableAsync();

        await db.ExecuteSqlAsync("SELECT sp_cancel_purchase(@pid)", ("pid", purchaseId));

        var act = () => db.ExecuteSqlAsync("SELECT sp_cancel_purchase(@pid)", ("pid", purchaseId));
        await act.Should().NotThrowAsync();
    }
}
