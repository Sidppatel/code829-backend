using Db.Entities;
using Db.Entities.Views;
using Db.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Db;

public class EventPlatformDbContext(
    DbContextOptions<EventPlatformDbContext> options,
    ChangeTrackingInterceptor changeTrackingInterceptor
) : DbContext(options)
{
    // Core entities
    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    // Template/parent entities
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TableType> TableTypes => Set<TableType>();
    public DbSet<TicketTypeTemplate> TicketTypeTemplates => Set<TicketTypeTemplate>();
    public DbSet<VenueLayout> VenueLayouts => Set<VenueLayout>();
    public DbSet<VenueLayoutTable> VenueLayoutTables => Set<VenueLayoutTable>();
    public DbSet<PricingRuleTemplate> PricingRuleTemplates => Set<PricingRuleTemplate>();
    public DbSet<EventTemplate> EventTemplates => Set<EventTemplate>();

    // Instance/child entities
    public DbSet<Event> Events => Set<Event>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();

    // Logging
    public DbSet<DeveloperLog> DeveloperLogs => Set<DeveloperLog>();
    public DbSet<AdminLog> AdminLogs => Set<AdminLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    // Read-only views
    public DbSet<EventView> EventViews => Set<EventView>();
    public DbSet<EventSummaryView> EventSummaryViews => Set<EventSummaryView>();
    public DbSet<TicketTypeView> TicketTypeViews => Set<TicketTypeView>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<PricingRuleView> PricingRuleViews => Set<PricingRuleView>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(changeTrackingInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── DB-side defaults for all BaseEntity tables ──────────
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType, entity =>
                {
                    entity.Property("Id").HasDefaultValueSql("gen_random_uuid()");
                    entity.Property("CreatedAt").HasDefaultValueSql("now()");
                    entity.Property("UpdatedAt").HasDefaultValueSql("now()");
                });
            }
        }

        // ─── Core entities ───────────────────────────────────────

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("addresses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Line1).HasMaxLength(512);
            entity.Property(e => e.Line2).HasMaxLength(512);
            entity.Property(e => e.City).HasMaxLength(128);
            entity.Property(e => e.State).HasMaxLength(2);
            entity.Property(e => e.ZipCode).HasMaxLength(10);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
            {
                t.HasCheckConstraint("CK_users_Role",
                    "\"Role\" IN ('User','Staff','Admin','Developer')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmailHash).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailHash).HasMaxLength(128);
            entity.Property(e => e.FirstName).HasMaxLength(128);
            entity.Property(e => e.LastName).HasMaxLength(128);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Address).WithMany().HasForeignKey(e => e.AddressId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("app_settings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.EncryptedValue).HasMaxLength(4096);
            entity.Property(e => e.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<MagicLinkToken>(entity =>
        {
            entity.ToTable("magic_link_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(256);
        });

        // ─── Template/parent entities ────────────────────────────

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(4096);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Website).HasMaxLength(512);
            entity.HasOne(e => e.Address).WithMany().HasForeignKey(e => e.AddressId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TableType>(entity =>
        {
            entity.ToTable("table_types", t =>
            {
                t.HasCheckConstraint("CK_table_types_DefaultShape",
                    "\"DefaultShape\" IN ('Round','Rectangle','Square','Cocktail')");
                t.HasCheckConstraint("CK_table_types_DefaultCapacity",
                    "\"DefaultCapacity\" > 0");
                t.HasCheckConstraint("CK_table_types_DefaultPriceCents",
                    "\"DefaultPriceCents\" >= 0");
                t.HasCheckConstraint("CK_table_types_PlatformFeeCents",
                    "\"PlatformFeeCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.DefaultShape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DefaultColor).HasMaxLength(20);
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenueId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TicketTypeTemplate>(entity =>
        {
            entity.ToTable("ticket_type_templates", t =>
            {
                t.HasCheckConstraint("CK_ticket_type_templates_DefaultPriceCents",
                    "\"DefaultPriceCents\" >= 0");
                t.HasCheckConstraint("CK_ticket_type_templates_DefaultPlatformFeeCents",
                    "\"DefaultPlatformFeeCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<VenueLayout>(entity =>
        {
            entity.ToTable("venue_layouts", t =>
            {
                t.HasCheckConstraint("CK_venue_layouts_LayoutMode",
                    "\"LayoutMode\" IN ('None','Grid','CapacityOnly')");
                t.HasCheckConstraint("CK_venue_layouts_EditorMode",
                    "\"EditorMode\" IS NULL OR \"EditorMode\" IN ('Grid')");
                t.HasCheckConstraint("CK_venue_layouts_GridDimensions",
                    "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VenueId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.LayoutMode).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.EditorMode).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VenueLayoutTable>(entity =>
        {
            entity.ToTable("venue_layout_tables", t =>
            {
                t.HasCheckConstraint("CK_venue_layout_tables_PriceType",
                    "\"PriceType\" IN ('PerTable','PerSeat')");
                t.HasCheckConstraint("CK_venue_layout_tables_PriceCents",
                    "\"PriceCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VenueLayoutId, e.Label }).IsUnique();
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.Property(e => e.Section).HasMaxLength(64);
            entity.Property(e => e.PriceType).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.VenueLayout).WithMany(vl => vl.Tables).HasForeignKey(e => e.VenueLayoutId);
            entity.HasOne(e => e.TableType).WithMany().HasForeignKey(e => e.TableTypeId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PricingRuleTemplate>(entity =>
        {
            entity.ToTable("pricing_rule_templates", t =>
            {
                t.HasCheckConstraint("CK_pricing_rule_templates_Type",
                    "\"Type\" IN ('Standard','EarlyBird','FirstN')");
                t.HasCheckConstraint("CK_pricing_rule_templates_DefaultPriceCents",
                    "\"DefaultPriceCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<EventTemplate>(entity =>
        {
            entity.ToTable("event_templates", t =>
            {
                t.HasCheckConstraint("CK_event_templates_Category",
                    "\"Category\" IS NULL OR \"Category\" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
                t.HasCheckConstraint("CK_event_templates_LayoutMode",
                    "\"LayoutMode\" IS NULL OR \"LayoutMode\" IN ('None','Grid','CapacityOnly')");
                t.HasCheckConstraint("CK_event_templates_DefaultMaxCapacity",
                    "\"DefaultMaxCapacity\" IS NULL OR \"DefaultMaxCapacity\" > 0");
                t.HasCheckConstraint("CK_event_templates_DefaultPlatformFeePercent",
                    "\"DefaultPlatformFeePercent\" IS NULL OR (\"DefaultPlatformFeePercent\" >= 0 AND \"DefaultPlatformFeePercent\" <= 100)");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.LayoutMode).HasConversion<string>().HasMaxLength(20);
        });

        // ─── Instance/child entities ─────────────────────────────

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events", t =>
            {
                t.HasCheckConstraint("CK_events_Status",
                    "\"Status\" IN ('Draft','Published','Completed','Cancelled')");
                t.HasCheckConstraint("CK_events_Category",
                    "\"Category\" IS NULL OR \"Category\" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
                t.HasCheckConstraint("CK_events_LayoutMode",
                    "\"LayoutMode\" IS NULL OR \"LayoutMode\" IN ('None','Grid','CapacityOnly')");
                t.HasCheckConstraint("CK_events_EditorMode",
                    "\"EditorMode\" IS NULL OR \"EditorMode\" IN ('Grid')");
                t.HasCheckConstraint("CK_events_DateRange",
                    "\"EndDate\" > \"StartDate\"");
                t.HasCheckConstraint("CK_events_MaxCapacity",
                    "\"MaxCapacity\" IS NULL OR \"MaxCapacity\" > 0");
                t.HasCheckConstraint("CK_events_PlatformFeePercent",
                    "\"PlatformFeePercent\" IS NULL OR (\"PlatformFeePercent\" >= 0 AND \"PlatformFeePercent\" <= 100)");
                t.HasCheckConstraint("CK_events_GridDimensions",
                    "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => new { e.Status, e.StartDate });
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.Slug).HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(8192);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.Property(e => e.LayoutMode).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.EditorMode).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Venue).WithMany(v => v.Events).HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Organizer).WithMany().HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EventTemplate).WithMany(et => et.Events).HasForeignKey(e => e.EventTemplateId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.VenueLayout).WithMany(vl => vl.Events).HasForeignKey(e => e.VenueLayoutId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
#pragma warning disable CS8603
            entity.HasGeneratedTsVectorColumn(e => e.SearchVector, "english", e => new { e.Title, Description = e.Description! })
                  .HasIndex(e => e.SearchVector).HasMethod("GIN");
#pragma warning restore CS8603
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.ToTable("ticket_types", t =>
            {
                t.HasCheckConstraint("CK_ticket_types_PriceCents",
                    "\"PriceCents\" IS NULL OR \"PriceCents\" >= 0");
                t.HasCheckConstraint("CK_ticket_types_QuantityTotal",
                    "\"QuantityTotal\" >= 0");
                t.HasCheckConstraint("CK_ticket_types_QuantitySold",
                    "\"QuantitySold\" >= 0 AND \"QuantitySold\" <= \"QuantityTotal\"");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.HasOne(e => e.Event).WithMany(ev => ev.TicketTypes).HasForeignKey(e => e.EventId);
            entity.HasOne(e => e.Template).WithMany(t => t.TicketTypes).HasForeignKey(e => e.TemplateId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Table>(entity =>
        {
            entity.ToTable("tables", t =>
            {
                t.HasCheckConstraint("CK_tables_Shape",
                    "\"Shape\" IS NULL OR \"Shape\" IN ('Round','Rectangle','Square','Cocktail')");
                t.HasCheckConstraint("CK_tables_PriceType",
                    "\"PriceType\" IN ('PerTable','PerSeat')");
                t.HasCheckConstraint("CK_tables_PriceCents",
                    "\"PriceCents\" >= 0");
                t.HasCheckConstraint("CK_tables_PriceOverrideCents",
                    "\"PriceOverrideCents\" IS NULL OR \"PriceOverrideCents\" >= 0");
                t.HasCheckConstraint("CK_tables_Capacity",
                    "\"Capacity\" IS NULL OR \"Capacity\" > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => new { e.EventId, e.Label }).IsUnique()
                .HasFilter("\"EventId\" IS NOT NULL");
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.Property(e => e.Shape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.Section).HasMaxLength(64);
            entity.Property(e => e.PriceType).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.TableType).WithMany(tt => tt.Tables).HasForeignKey(e => e.TableTypeId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VenueLayoutTable).WithMany(vlt => vlt.Tables).HasForeignKey(e => e.VenueLayoutTableId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.ToTable("seats", t =>
            {
                t.HasCheckConstraint("CK_seats_SeatNumber",
                    "\"SeatNumber\" > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TableId, e.SeatNumber }).IsUnique();
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.HasOne(e => e.Table).WithMany(t => t.Seats).HasForeignKey(e => e.TableId);
        });

        modelBuilder.Entity<SeatHold>(entity =>
        {
            entity.ToTable("seat_holds");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SeatId, e.EventId })
                .IsUnique().HasFilter("\"IsActive\" = true");
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.IsActive, e.ExpiresAt })
                .HasFilter("\"IsActive\" = true");
            entity.HasOne(e => e.Seat).WithMany(s => s.Holds).HasForeignKey(e => e.SeatId);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TicketType).WithMany().HasForeignKey(e => e.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("CK_bookings_Status",
                    "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded')");
                t.HasCheckConstraint("CK_bookings_SubtotalCents",
                    "\"SubtotalCents\" >= 0");
                t.HasCheckConstraint("CK_bookings_FeeCents",
                    "\"FeeCents\" >= 0");
                t.HasCheckConstraint("CK_bookings_TotalCents",
                    "\"TotalCents\" >= 0");
                t.HasCheckConstraint("CK_bookings_TotalFormula",
                    "\"TotalCents\" = \"SubtotalCents\" + \"FeeCents\"");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BookingNumber).IsUnique();
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("\"QrToken\" IS NOT NULL");
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.EventId, e.Status });
            entity.Property(e => e.BookingNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.Notes).HasMaxLength(1024);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.ToTable("booking_items", t =>
            {
                t.HasCheckConstraint("CK_booking_items_PriceCents",
                    "\"PriceCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("\"QrToken\" IS NOT NULL");
            entity.HasIndex(e => e.InvitationToken).IsUnique().HasFilter("\"InvitationToken\" IS NOT NULL");
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.GuestName).HasMaxLength(256);
            entity.Property(e => e.GuestEmail).HasMaxLength(256);
            entity.Property(e => e.InvitationToken).HasMaxLength(128);
            entity.HasOne(e => e.Booking).WithMany(b => b.Items).HasForeignKey(e => e.BookingId);
            entity.HasOne(e => e.TicketType).WithMany().HasForeignKey(e => e.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Seat).WithMany().HasForeignKey(e => e.SeatId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments", t =>
            {
                t.HasCheckConstraint("CK_payments_Status",
                    "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                t.HasCheckConstraint("CK_payments_AmountCents",
                    "\"AmountCents\" >= 0");
                t.HasCheckConstraint("CK_payments_Currency",
                    "\"Currency\" IN ('usd')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentIntentId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.PaidAt });
            entity.Property(e => e.PaymentIntentId).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.RefundId).HasMaxLength(128);
            entity.HasOne(e => e.Booking).WithOne(b => b.Payment).HasForeignKey<Payment>(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.ToTable("pricing_rules", t =>
            {
                t.HasCheckConstraint("CK_pricing_rules_Type",
                    "\"Type\" IS NULL OR \"Type\" IN ('Standard','EarlyBird','FirstN')");
                t.HasCheckConstraint("CK_pricing_rules_PriceCents",
                    "\"PriceCents\" IS NULL OR \"PriceCents\" >= 0");
                t.HasCheckConstraint("CK_pricing_rules_DateRange",
                    "\"ValidFrom\" IS NULL OR \"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
                t.HasCheckConstraint("CK_pricing_rules_MaxCount",
                    "\"MaxCount\" IS NULL OR \"MaxCount\" > 0");
                t.HasCheckConstraint("CK_pricing_rules_UsedCount",
                    "\"UsedCount\" >= 0");
                t.HasCheckConstraint("CK_pricing_rules_FeePercent",
                    "\"FeePercent\" IS NULL OR (\"FeePercent\" >= 0 AND \"FeePercent\" <= 100)");
                t.HasCheckConstraint("CK_pricing_rules_FeeFlatCents",
                    "\"FeeFlatCents\" IS NULL OR \"FeeFlatCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId);
            entity.HasOne(e => e.TableType).WithMany().HasForeignKey(e => e.TableTypeId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Template).WithMany(t => t.PricingRules).HasForeignKey(e => e.TemplateId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ─── Logging ─────────────────────────────────────────────

        modelBuilder.Entity<DeveloperLog>(entity =>
        {
            entity.ToTable("developer_logs", t =>
            {
                t.HasCheckConstraint("CK_developer_logs_Severity",
                    "\"Severity\" IN ('Warning','Error','Critical')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Severity);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Message).HasMaxLength(4096);
            entity.Property(e => e.ExceptionType).HasMaxLength(512);
            entity.Property(e => e.RequestPath).HasMaxLength(512);
            entity.Property(e => e.RequestMethod).HasMaxLength(10);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.CorrelationId).HasMaxLength(64);
        });

        modelBuilder.Entity<AdminLog>(entity =>
        {
            entity.ToTable("admin_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Action);
            entity.Property(e => e.Action).HasMaxLength(128);
            entity.Property(e => e.ActorEmail).HasMaxLength(256);
            entity.Property(e => e.ActorRole).HasMaxLength(20);
            entity.Property(e => e.EntityType).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(2048);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.ToTable("system_logs", t =>
            {
                t.HasCheckConstraint("CK_system_logs_Category",
                    "\"Category\" IN ('EntityChange','BackgroundWorker','Cache','MockService','Migration')");
                t.HasCheckConstraint("CK_system_logs_DurationMs",
                    "\"DurationMs\" IS NULL OR \"DurationMs\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Category);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Action).HasMaxLength(64);
            entity.Property(e => e.Source).HasMaxLength(256);
            entity.Property(e => e.EntityType).HasMaxLength(64);
            entity.Property(e => e.CorrelationId).HasMaxLength(64);
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("email_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Recipient).HasMaxLength(256);
            entity.Property(e => e.Subject).HasMaxLength(512);
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        // ─── Read-only views ─────────────────────────────────────

        modelBuilder.Entity<EventView>(entity =>
        {
            entity.ToView("v_events");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<EventSummaryView>(entity =>
        {
            entity.ToView("v_event_summary");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<TicketTypeView>(entity =>
        {
            entity.ToView("v_ticket_types");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<TableView>(entity =>
        {
            entity.ToView("v_tables");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<PricingRuleView>(entity =>
        {
            entity.ToView("v_pricing_rules");
            entity.HasKey(e => e.Id);
        });
    }
}
