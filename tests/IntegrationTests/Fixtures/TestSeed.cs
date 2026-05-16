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
        var email = $"admin-{id}@test.com";
        await db.ExecuteSqlAsync("""
            INSERT INTO public.business_users ("Id","Email","EmailHash","FirstName","LastName","Role","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @email, @emailHash, 'Test', 'Admin', 'Admin', true, now(), now())
            """,
            ("id", id), ("email", email), ("emailHash", email.GetHashCode().ToString()));
        return id;
    }

    public static async Task<Guid> SeedVenueAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.venues ("Id","Name","Address","City","State","ZipCode","IsActive","CreatedAt","UpdatedAt")
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
            INSERT INTO public.events (
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
        var email = $"user-{id}@test.com";
        await db.ExecuteSqlAsync("""
            INSERT INTO public.users ("Id","Email","EmailHash","FirstName","LastName","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @email, @emailHash, 'Test', 'User', true, now(), now())
            """,
            ("id", id), ("email", email), ("emailHash", email.GetHashCode().ToString()));
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
            INSERT INTO public.organizations (
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
            INSERT INTO public.business_users (
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

    public static async Task<Guid> SeedPurchaseAsync(DatabaseFixture db, Guid userId, Guid eventId, string status = "Confirmed")
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.purchases (
                "Id","UserId","EventId","Status","Seats",
                "SubtotalCents","FeeCents","TotalCents","PurchaseNumber","CreatedAt","UpdatedAt")
            VALUES (@id, @uid, @ev, @status, 1, 1000, 50, 1050, @pnum, now(), now())
            """,
            ("id", id), ("uid", userId), ("ev", eventId), ("status", status),
            ("pnum", $"PUR-{id.ToString()[..8].ToUpper()}"));
        return id;
    }

    public static async Task<Guid> SeedTicketAsync(DatabaseFixture db, Guid purchaseId, Guid eventId, Guid userId, string? qrToken = null)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.tickets (
                "Id","PurchaseId","EventId","BuyerUserId","Status",
                "QrToken","CreatedAt","UpdatedAt")
            VALUES (@id, @pid, @ev, @uid, 'Active', @qr, now(), now())
            """,
            ("id", id), ("pid", purchaseId), ("ev", eventId), ("uid", userId),
            ("qr", qrToken ?? $"qr-{id.ToString()[..8]}"));
        return id;
    }

    public static async Task<Guid> SeedTableAsync(DatabaseFixture db, Guid eventId, string status = "Available")
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.tables ("Id","EventId","Name","Status","Capacity","CreatedAt","UpdatedAt")
            VALUES (@id, @ev, @name, @status, 8, now(), now())
            """,
            ("id", id), ("ev", eventId), ("name", $"Table {id.ToString()[..4]}"), ("status", status));
        return id;
    }

    public static async Task SeedPurchaseTableAsync(DatabaseFixture db, Guid purchaseId, Guid tableId)
    {
        await db.ExecuteSqlAsync("""
            INSERT INTO public.purchase_tables ("PurchaseId","TableId")
            VALUES (@pid, @tid)
            """,
            ("pid", purchaseId), ("tid", tableId));
    }

    public static async Task<Guid> SeedEventTicketTypeAsync(DatabaseFixture db, Guid eventId, int quota = 10)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.event_ticket_types
                ("Id","EventId","Name","PriceCents","Quota","SoldCount","CreatedAt","UpdatedAt")
            VALUES (@id, @ev, 'General', 1000, @quota, 0, now(), now())
            """,
            ("id", id), ("ev", eventId), ("quota", quota));
        return id;
    }
}
