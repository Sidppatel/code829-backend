using Npgsql;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Shared raw-SQL seed helpers for integration tests. tests/ is whitelisted from
/// the Data Access Rule (see CLAUDE.md), so direct INSERTs are allowed here.
/// </summary>
public static class TestSeed
{
    public record EventOptions(
        string LayoutMode = "Open",
        int? MaxCapacity = 100,
        string Status = "Published",
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        DateTime? ScheduledPublishAt = null);

    public static async Task<Guid> SeedBusinessUserAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO business_users ("Id","Email","FirstName","LastName","Role","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Test', 'Admin', 'Admin', true, now(), now())
            """,
            ("id", id), ("email", $"admin-{id}@test.com"));
        return id;
    }

    public static async Task<Guid> SeedVenueAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO venues ("Id","Name","Address","City","State","ZipCode","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, 'Test Venue', '1 Test St', 'Testville', 'TS', '00000', true, now(), now())
            """,
            ("id", id));
        return id;
    }

    public static async Task<Guid> SeedEventAsync(DatabaseFixture db, EventOptions? opts = null)
    {
        opts ??= new EventOptions();
        var venueId = await SeedVenueAsync(db);
        var businessUserId = await SeedBusinessUserAsync(db);

        var id = Guid.NewGuid();
        var start = opts.StartDate ?? DateTime.UtcNow.AddDays(7);
        var end = opts.EndDate ?? DateTime.UtcNow.AddDays(8);
        var publishedAt = opts.Status == "Published" ? (DateTime?)DateTime.UtcNow.AddMinutes(-5) : null;

        await db.ExecuteSqlAsync("""
            INSERT INTO events (
                "Id","Title","Slug","Description","Status","StartDate","EndDate",
                "IsFeatured","LayoutMode","MaxCapacity","PublishedAt","ScheduledPublishAt",
                "VenueId","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@id, @title, @slug, 'test', @status, @start, @end,
                false, @layout, @maxCap, @publishedAt, @scheduledAt,
                @venue, @business, now(), now())
            """,
            ("id", id),
            ("title", $"Test Event {id}"),
            ("slug", $"test-event-{id}"),
            ("status", opts.Status),
            ("start", start),
            ("end", end),
            ("layout", opts.LayoutMode),
            ("maxCap", (object?)opts.MaxCapacity ?? DBNull.Value),
            ("publishedAt", (object?)publishedAt ?? DBNull.Value),
            ("scheduledAt", (object?)opts.ScheduledPublishAt ?? DBNull.Value),
            ("venue", venueId),
            ("business", businessUserId));
        return id;
    }

    public static async Task<Guid> SeedUserAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO users ("Id","Email","FirstName","LastName","Role","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Test', 'User', 'User', true, now(), now())
            """,
            ("id", id), ("email", $"user-{id}@test.com"));
        return id;
    }

    /// <summary>
    /// Seeds an Organization. <paramref name="stripeAccountId"/> is optional —
    /// supply one when the test wants to assert webhook handlers find the org
    /// by acct id.
    /// </summary>
    public static async Task<Guid> SeedOrganizationAsync(
        DatabaseFixture db,
        string? stripeAccountId = null,
        bool chargesEnabled = false,
        bool payoutsEnabled = false,
        bool detailsSubmitted = false,
        string countryCode = "US")
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO organizations (
                "Id","Name","CountryCode","StripeConnectedAccountId",
                "StripeChargesEnabled","StripePayoutsEnabled","StripeDetailsSubmitted",
                "CreatedAt","UpdatedAt")
            VALUES (@id, @name, @cc, @acct, @ce, @pe, @ds, now(), now())
            """,
            ("id", id),
            ("name", $"Test Org {id.ToString()[..8]}"),
            ("cc", countryCode),
            ("acct", (object?)stripeAccountId ?? DBNull.Value),
            ("ce", chargesEnabled),
            ("pe", payoutsEnabled),
            ("ds", detailsSubmitted));
        return id;
    }

    /// <summary>
    /// Seeds an admin BusinessUser optionally attached to an Organization.
    /// Returns the BusinessUser id; the caller can use it to mint a JWT via
    /// AuthHelper.GenerateAdminJwt for endpoint-level auth tests.
    /// </summary>
    public static async Task<Guid> SeedBusinessUserWithOrgAsync(
        DatabaseFixture db,
        Guid? organizationId = null,
        string role = "Admin",
        string? email = null)
    {
        var id = Guid.NewGuid();
        var emailValue = email ?? $"bu-{id}@test.com";
        await db.ExecuteSqlAsync("""
            INSERT INTO business_users (
                "Id","Email","EmailHash","FirstName","LastName","Role","IsActive",
                "PasswordHash","OrganizationId","FailedLoginAttempts","CreatedAt","UpdatedAt")
            VALUES (@id, @email, @emailHash, 'Test', 'Admin', @role, true,
                    'bcrypt-test-hash', @orgId, 0, now(), now())
            """,
            ("id", id),
            ("email", emailValue),
            ("emailHash", emailValue.GetHashCode().ToString()),
            ("role", role),
            ("orgId", (object?)organizationId ?? DBNull.Value));
        return id;
    }
}
