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
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

    // Template/parent entities
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TableTemplate> TableTemplates => Set<TableTemplate>();

    // Instance/child entities
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventTable> EventTables => Set<EventTable>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingTicket> BookingTickets => Set<BookingTicket>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Images
    public DbSet<Image> Images => Set<Image>();

    // User-facing
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    // Logging
    public DbSet<DeveloperLog> DeveloperLogs => Set<DeveloperLog>();
    public DbSet<AdminLog> AdminLogs => Set<AdminLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    // Read-only views
    public DbSet<EventView> EventViews => Set<EventView>();
    public DbSet<EventSummaryView> EventSummaryViews => Set<EventSummaryView>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<BookingView> BookingViews => Set<BookingView>();
    public DbSet<BookingTicketView> BookingTicketViews => Set<BookingTicketView>();
    public DbSet<VenueView> VenueViews => Set<VenueView>();
    public DbSet<UserProfileView> UserProfileViews => Set<UserProfileView>();
    public DbSet<EventTablesSummaryView> EventTablesSummaryViews => Set<EventTablesSummaryView>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(changeTrackingInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");

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
            entity.Property(e => e.AvatarPath).HasMaxLength(512);
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
            entity.ToTable("magic_link_tokens", t =>
            {
                t.HasCheckConstraint("CK_magic_link_tokens_Usage",
                    "(\"IsUsed\" = false AND \"UsedAt\" IS NULL) OR (\"IsUsed\" = true AND \"UsedAt\" IS NOT NULL)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<DeviceSession>(entity =>
        {
            entity.ToTable("device_sessions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.ExpiresAt, e.RevokedAt })
                .HasFilter("\"RevokedAt\" IS NULL")
                .HasDatabaseName("IX_device_sessions_Active");
            entity.Property(e => e.SessionHash).HasMaxLength(128);
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(256);
            entity.Property(e => e.DeviceName).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<TableTemplate>(entity =>
        {
            entity.ToTable("table_templates", t =>
            {
                t.HasCheckConstraint("CK_table_templates_DefaultShape",
                    "\"DefaultShape\" IN ('Round','Rectangle','Square','Cocktail')");
                t.HasCheckConstraint("CK_table_templates_DefaultCapacity",
                    "\"DefaultCapacity\" > 0");
                t.HasCheckConstraint("CK_table_templates_DefaultPriceCents",
                    "\"DefaultPriceCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.DefaultShape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DefaultColor).HasMaxLength(20);
        });

        modelBuilder.Entity<EventTable>(entity =>
        {
            entity.ToTable("event_tables", t =>
            {
                t.HasCheckConstraint("CK_event_tables_Shape",
                    "\"Shape\" IN ('Round','Rectangle','Square','Cocktail')");
                t.HasCheckConstraint("CK_event_tables_Capacity",
                    "\"Capacity\" > 0");
                t.HasCheckConstraint("CK_event_tables_PriceCents",
                    "\"PriceCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EventId, e.Label });
            entity.Property(e => e.Label).HasMaxLength(128);
            entity.Property(e => e.Shape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.TableTemplate).WithMany(tt => tt.EventTables)
                .HasForeignKey(e => e.TableTemplateId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
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
                    "\"LayoutMode\" IN ('Grid','Open')");
                t.HasCheckConstraint("CK_events_DateRange",
                    "\"EndDate\" > \"StartDate\"");
                t.HasCheckConstraint("CK_events_MaxCapacity",
                    "\"MaxCapacity\" IS NULL OR \"MaxCapacity\" > 0");
                t.HasCheckConstraint("CK_events_PricePerPersonCents",
                    "\"PricePerPersonCents\" IS NULL OR \"PricePerPersonCents\" >= 0");
                t.HasCheckConstraint("CK_events_PlatformFeePercent",
                    "\"PlatformFeePercent\" IS NULL OR (\"PlatformFeePercent\" >= 0 AND \"PlatformFeePercent\" <= 100)");
                t.HasCheckConstraint("CK_events_GridDimensions",
                    "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");
                t.HasCheckConstraint("CK_events_PublishLifecycle",
                    "\"Status\" <> 'Published' OR \"PublishedAt\" IS NOT NULL");
                t.HasCheckConstraint("CK_events_DraftNoPublishDate",
                    "\"Status\" <> 'Draft' OR \"PublishedAt\" IS NULL");
                t.HasCheckConstraint("CK_events_CompletedRequiresPublish",
                    "\"Status\" <> 'Completed' OR \"PublishedAt\" IS NOT NULL");
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
            entity.HasOne(e => e.Venue).WithMany(v => v.Events).HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Organizer).WithMany().HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);
#pragma warning disable CS8603
            entity.HasGeneratedTsVectorColumn(e => e.SearchVector, "english", e => new { e.Title, Description = e.Description! })
                  .HasIndex(e => e.SearchVector).HasMethod("GIN");
#pragma warning restore CS8603
        });

        modelBuilder.Entity<Table>(entity =>
        {
            entity.ToTable("tables", t =>
            {
                t.HasCheckConstraint("CK_tables_Status",
                    "\"Status\" IN ('Available','Locked','Booked')");
                t.HasCheckConstraint("CK_tables_LockedRequiresOwner",
                    "\"Status\" <> 'Locked' OR (\"LockedByUserId\" IS NOT NULL AND \"LockExpiresAt\" IS NOT NULL)");
                t.HasCheckConstraint("CK_tables_AvailableNoLock",
                    "\"Status\" <> 'Available' OR (\"LockedByUserId\" IS NULL AND \"LockExpiresAt\" IS NULL)");
                t.HasCheckConstraint("CK_tables_GridRow",
                    "\"GridRow\" >= 0");
                t.HasCheckConstraint("CK_tables_GridCol",
                    "\"GridCol\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => new { e.EventId, e.Label }).IsUnique();
            entity.HasIndex(e => new { e.EventId, e.GridRow, e.GridCol }).IsUnique();
            entity.HasIndex(e => new { e.EventId, e.Status })
                .HasDatabaseName("IX_tables_EventId_Status");
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(Contracts.Enums.TableStatus.Available);
            entity.HasOne(e => e.EventTable).WithMany(et => et.Tables)
                .HasForeignKey(e => e.EventTableId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.LockedByUser).WithMany().HasForeignKey(e => e.LockedByUserId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("CK_bookings_Status",
                    "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                t.HasCheckConstraint("CK_bookings_SubtotalCents",
                    "\"SubtotalCents\" >= 0");
                t.HasCheckConstraint("CK_bookings_FeeCents",
                    "\"FeeCents\" >= 0");
                t.HasCheckConstraint("CK_bookings_TotalCents",
                    "\"TotalCents\" >= 0");
                t.HasCheckConstraint("CK_bookings_TotalFormula",
                    "\"TotalCents\" = \"SubtotalCents\" + \"FeeCents\"");
                t.HasCheckConstraint("CK_bookings_SeatsReserved",
                    "\"SeatsReserved\" IS NULL OR \"SeatsReserved\" > 0");
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
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Table).WithMany().HasForeignKey(e => e.TableId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BookingTicket>(entity =>
        {
            entity.ToTable("booking_tickets", t =>
            {
                t.HasCheckConstraint("CK_booking_tickets_Status",
                    "\"Status\" IN ('Unassigned','Invited','Claimed','CheckedIn')");
                t.HasCheckConstraint("CK_booking_tickets_SeatNumber",
                    "\"SeatNumber\" > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QrToken).IsUnique();
            entity.HasIndex(e => e.InviteTokenHash).IsUnique()
                .HasFilter("\"InviteTokenHash\" IS NOT NULL");
            entity.HasIndex(e => new { e.BookingId, e.SeatNumber }).IsUnique();
            entity.HasIndex(e => e.GuestUserId);
            entity.Property(e => e.TicketCode).HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.InviteTokenHash).HasMaxLength(128);
            entity.Property(e => e.InvitedEmail).HasMaxLength(256);
            entity.HasOne(e => e.Booking).WithMany(b => b.Tickets)
                .HasForeignKey(e => e.BookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GuestUser).WithMany()
                .HasForeignKey(e => e.GuestUserId).IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
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
                t.HasCheckConstraint("CK_payments_RefundLifecycle",
                    "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");
                t.HasCheckConstraint("CK_payments_PaidLifecycle",
                    "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");
                t.HasCheckConstraint("CK_payments_PendingNoPaidDate",
                    "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");
                t.HasCheckConstraint("CK_payments_NotRefundedNoRefundDate",
                    "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");
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

        // ─── Images ──────────────────────────────────────────────

        modelBuilder.Entity<Image>(entity =>
        {
            entity.ToTable("images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.Property(e => e.EntityType).HasMaxLength(20);
            entity.Property(e => e.StorageKey).HasMaxLength(500);
            entity.Property(e => e.OriginalName).HasMaxLength(255);
            entity.HasOne(e => e.UploadedBy).WithMany().HasForeignKey(e => e.UploadedById)
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

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.ToTable("feedbacks");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.Message).HasMaxLength(2000);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Type);
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

        modelBuilder.Entity<TableView>(entity =>
        {
            entity.ToView("v_tables");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<BookingView>(entity =>
        {
            entity.ToView("v_bookings");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<BookingTicketView>(entity =>
        {
            entity.ToView("v_booking_tickets");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<VenueView>(entity =>
        {
            entity.ToView("v_venues");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<UserProfileView>(entity =>
        {
            entity.ToView("v_user_profile");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<EventTablesSummaryView>(entity =>
        {
            entity.ToView("v_event_tables_summary");
            entity.HasKey(e => e.Id);
        });
    }
}
