using Npgsql;

namespace IntegrationTests.Fixtures;

public static class TestSeed
{
    public record EventOptions(
        string LayoutMode = "Open",
        int? MaxCapacity = 100,
        bool IsPublished = true,
        string Status = "Published",
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        DateTime? ScheduledPublishAt = null);

    public static async Task<Guid> SeedBusinessUserAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        var email = $"admin-{id}@test.com";
        await db.ExecuteSqlAsync("""
            INSERT INTO public.business_users ("Id","Email","EmailHash","FirstName","LastName","Role","IsActive","PasswordHash","FailedLoginAttempts","CreatedAt","UpdatedAt")
            VALUES (@id, @email, @emailHash, 'Test', 'Admin', 'Admin', true, 'bcrypt-hash', 0, now(), now())
            """,
            ("id", id), ("email", email), ("emailHash", email.GetHashCode().ToString()));
        return id;
    }

    public static async Task<Guid> SeedVenueAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.venues ("Id","Name","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @name, true, now(), now())
            """,
            ("id", id), ("name", $"Venue {id.ToString()[..4]}"));
        return id;
    }

    public static async Task<Guid> SeedEventAsync(DatabaseFixture db, EventOptions? opts = null)
    {
        opts ??= new EventOptions();
        var id = Guid.NewGuid();
        var orgId = await SeedOrganizationAsync(db);
        var venueId = await SeedVenueAsync(db);
        var businessUserId = await SeedBusinessUserWithOrgAsync(db, orgId);
        var publishedAt = opts.IsPublished ? DateTime.UtcNow : (DateTime?)null;

        await db.ExecuteSqlAsync("""
            INSERT INTO public.events (
                "Id","Title","Slug","Status","Category","IsFeatured",
                "StartDate","EndDate","MaxCapacity","LayoutMode",
                "PublishedAt","ScheduledPublishAt","VenueId","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@id, @title, @slug, @status, 'Music', false,
                    @start, @end, @max, @layout,
                    @publishedAt, @scheduledAt, @venue, @business, now(), now())
            """,
            ("id", id),
            ("title", $"Event {id.ToString()[..4]}"),
            ("slug", $"event-{id.ToString()[..4]}"),
            ("status", opts.Status),
            ("start", DateTime.UtcNow.AddDays(1)),
            ("end", DateTime.UtcNow.AddDays(1).AddHours(4)),
            ("max", opts.MaxCapacity),
            ("layout", opts.LayoutMode),
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
            INSERT INTO public.users ("Id","Email","EmailHash","FirstName","LastName","IsActive","CreatedAt","UpdatedAt","OptInLocationEmail","HasCompletedOnboarding")
            VALUES (@id, @email, @emailHash, 'Test', 'User', true, now(), now(), false, true)
            """,
            ("id", id), ("email", email), ("emailHash", email.GetHashCode().ToString()));
        return id;
    }

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

    public static async Task<Guid> SeedPurchaseAsync(DatabaseFixture db, Guid userId, Guid eventId, string status = "Paid")
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.purchases (
                "Id","UserId","EventId","Status","SeatsReserved",
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
            INSERT INTO public.purchase_tickets (
                "Id","PurchaseId","Status","TicketCode","SeatNumber",
                "QrToken","CreatedAt","UpdatedAt")
            VALUES (@id, @pid, 'Claimed', @tcode, 1, @qr, now(), now())
            """,
            ("id", id), ("pid", purchaseId),
            ("tcode", $"T-{id.ToString()[..8].ToUpper()}"),
            ("qr", qrToken ?? $"qr-{id.ToString()[..8]}"));
        return id;
    }

    public static async Task<Guid> SeedEventTableAsync(DatabaseFixture db, Guid eventId)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.event_tables ("Id","EventId","Label","Capacity","PriceCents","Shape","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @ev, 'Main Row', 8, 5000, 'Rectangle', true, now(), now())
            """,
            ("id", id), ("ev", eventId));
        return id;
    }

    public static async Task<Guid> SeedTableAsync(DatabaseFixture db, Guid eventId, Guid eventTableId, string status = "Available")
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO public.tables (
                "Id","EventId","EventTableId","Label","Status","GridRow","GridCol",
                "RowSpan","ColSpan","SortOrder","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @ev, @etid, @label, @status, 0, 0, 1, 1, 0, true, now(), now())
            """,
            ("id", id), ("ev", eventId), ("etid", eventTableId),
            ("label", $"T-{id.ToString()[..4]}"), ("status", status));
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
                ("Id","EventId","Label","PriceCents","MaxQuantity","SortOrder","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @ev, 'General', 1000, @quota, 0, true, now(), now())
            """,
            ("id", id), ("ev", eventId), ("quota", quota));
        return id;
    }
}
