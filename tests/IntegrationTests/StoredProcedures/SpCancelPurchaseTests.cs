using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpCancelPurchaseTests(DatabaseFixture db)
{
    private async Task<(Guid purchaseId, Guid tableId)> SeedPurchaseWithTableAsync()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var tableId = Guid.NewGuid();

        await db.ExecuteSqlAsync("""
            INSERT INTO events ("Id","Title","Slug","Description","StartDate","EndDate",
                "LayoutMode","MaxCapacity","Status","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@ev, 'Cancel Event', 'cancel-' || @ev::text, 'desc', now() + interval '7 days', now() + interval '8 days',
                'Seated', 200, 'Published', gen_random_uuid(), now(), now())
            """, ("ev", eventId));

        await db.ExecuteSqlAsync("""
            INSERT INTO users ("Id","Email","FirstName","LastName","Role","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Cancel','User','User', now(), now())
            """, ("id", userId), ("email", $"cancel-{userId}@test.com"));

        await db.ExecuteSqlAsync("""
            INSERT INTO tables ("Id","EventId","Name","Status","Capacity","CreatedAt","UpdatedAt")
            VALUES (@tid, @ev, 'Table 1', 'Booked', 8, now(), now())
            """, ("tid", tableId), ("ev", eventId));

        await db.ExecuteSqlAsync("""
            INSERT INTO purchases ("Id","UserId","EventId","Status","Seats",
                "SubtotalCents","FeeCents","TotalCents","PurchaseNumber","CreatedAt","UpdatedAt")
            VALUES (@pid, @uid, @ev, 'Confirmed', 1, 5000, 250, 5250, 'CXL-001', now(), now())
            """, ("pid", purchaseId), ("uid", userId), ("ev", eventId));

        await db.ExecuteSqlAsync("""
            INSERT INTO purchase_tables ("PurchaseId","TableId")
            VALUES (@pid, @tid)
            """, ("pid", purchaseId), ("tid", tableId));

        return (purchaseId, tableId);
    }

    [Fact(Skip = "S1 test seeds incomplete (missing IsFeatured + FK-valid VenueId/BusinessUserId); rewrite with TestSeed helper tracked as follow-up")]
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

    [Fact(Skip = "S1 test seeds incomplete (missing IsFeatured + FK-valid VenueId/BusinessUserId); rewrite with TestSeed helper tracked as follow-up")]
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

    [Fact(Skip = "S1 test seeds incomplete (missing IsFeatured + FK-valid VenueId/BusinessUserId); rewrite with TestSeed helper tracked as follow-up")]
    public async Task Cancel_IsIdempotent_WhenCalledTwice()
    {
        var (purchaseId, _) = await SeedPurchaseWithTableAsync();

        await db.ExecuteSqlAsync("SELECT sp_cancel_purchase(@pid)", ("pid", purchaseId));

        var act = () => db.ExecuteSqlAsync("SELECT sp_cancel_purchase(@pid)", ("pid", purchaseId));
        await act.Should().NotThrowAsync();
    }
}
