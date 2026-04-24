using Db.Entities;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;

namespace Db;

public class EventPlatformDbContext(
    DbContextOptions<EventPlatformDbContext> options
) : DbContext(options)
{
    // Core entities
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<BusinessPasswordResetToken> BusinessPasswordResetTokens => Set<BusinessPasswordResetToken>();
    public DbSet<UserPasswordResetToken> UserPasswordResetTokens => Set<UserPasswordResetToken>();
    public DbSet<UserEmailVerificationToken> UserEmailVerificationTokens => Set<UserEmailVerificationToken>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

    // Template/parent entities
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TableTemplate> TableTemplates => Set<TableTemplate>();

    // Instance/child entities
    public DbSet<Event> Events => Set<Event>();
    public DbSet<BusinessUserEvent> BusinessUserEvents => Set<BusinessUserEvent>();
    public DbSet<EventTable> EventTables => Set<EventTable>();
    public DbSet<EventTicketType> EventTicketTypes => Set<EventTicketType>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseTicket> PurchaseTickets => Set<PurchaseTicket>();
    public DbSet<PurchaseTable> PurchaseTables => Set<PurchaseTable>();
    public DbSet<StripeTransaction> StripeTransactions => Set<StripeTransaction>();

    // Images
    public DbSet<Image> Images => Set<Image>();
    public DbSet<EventImage> EventImages => Set<EventImage>();
    public DbSet<VenueImage> VenueImages => Set<VenueImage>();
    public DbSet<PlatformImage> PlatformImages => Set<PlatformImage>();

    // User-facing
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    // Logging
    public DbSet<DeveloperLog> DeveloperLogs => Set<DeveloperLog>();
    public DbSet<BusinessLog> BusinessLogs => Set<BusinessLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Read-only views
    public DbSet<EventView> EventViews => Set<EventView>();
    public DbSet<EventSummaryView> EventSummaryViews => Set<EventSummaryView>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<PurchaseView> PurchaseViews => Set<PurchaseView>();
    public DbSet<PurchaseTicketView> PurchaseTicketViews => Set<PurchaseTicketView>();
    public DbSet<VenueView> VenueViews => Set<VenueView>();
    public DbSet<UserProfileView> UserProfileViews => Set<UserProfileView>();
    public DbSet<EventTablesSummaryView> EventTablesSummaryViews => Set<EventTablesSummaryView>();
    public DbSet<EventTicketTypeSummaryView> EventTicketTypeSummaryViews => Set<EventTicketTypeSummaryView>();
    public DbSet<BusinessUserView> BusinessUserViews => Set<BusinessUserView>();
    public DbSet<BusinessUserEventView> BusinessUserEventViews => Set<BusinessUserEventView>();
    public DbSet<DeviceSessionView> DeviceSessionViews => Set<DeviceSessionView>();
    public DbSet<InvitationView> InvitationViews => Set<InvitationView>();
    public DbSet<FeedbackView> FeedbackViews => Set<FeedbackView>();
    public DbSet<EventImageView> EventImageViews => Set<EventImageView>();
    public DbSet<VenueImageView> VenueImageViews => Set<VenueImageView>();
    public DbSet<PlatformImageView> PlatformImageViews => Set<PlatformImageView>();
    public DbSet<BusinessLogView> BusinessLogViews => Set<BusinessLogView>();
    public DbSet<SystemLogView> SystemLogViews => Set<SystemLogView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("extensions", "pg_trgm");

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
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmailHash).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailHash).HasMaxLength(128);
            entity.Property(e => e.FirstName).HasMaxLength(128);
            entity.Property(e => e.LastName).HasMaxLength(128);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.EmailVerified).HasDefaultValue(false);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImageId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Address).WithMany().HasForeignKey(e => e.AddressId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BusinessUser>(entity =>
        {
            entity.ToTable("business_users", t =>
            {
                t.HasCheckConstraint("CK_business_users_Role",
                    "\"Role\" IN ('Staff','Admin','Developer')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmailHash).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailHash).HasMaxLength(128);
            entity.Property(e => e.FirstName).HasMaxLength(128);
            entity.Property(e => e.LastName).HasMaxLength(128);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImageId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations", t =>
            {
                t.HasCheckConstraint("CK_invitations_Status",
                    "\"Status\" IN ('Pending','Accepted','Revoked','Expired')");
                t.HasCheckConstraint("CK_invitations_Role",
                    "\"Role\" IN ('Staff','Admin','Developer')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.InvitedBy)
                .WithMany()
                .HasForeignKey(e => e.InvitedByBusinessUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("app_settings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).HasMaxLength(4096);
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

        modelBuilder.Entity<BusinessPasswordResetToken>(entity =>
        {
            entity.ToTable("business_password_reset_tokens", t =>
            {
                t.HasCheckConstraint("CK_business_password_reset_tokens_Usage",
                    "(\"IsUsed\" = false AND \"UsedAt\" IS NULL) OR (\"IsUsed\" = true AND \"UsedAt\" IS NOT NULL)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.BusinessUserId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.HasOne(e => e.BusinessUser).WithMany().HasForeignKey(e => e.BusinessUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPasswordResetToken>(entity =>
        {
            entity.ToTable("user_password_reset_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserEmailVerificationToken>(entity =>
        {
            entity.ToTable("user_email_verification_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceSession>(entity =>
        {
            entity.ToTable("device_sessions", t =>
            {
                t.HasCheckConstraint("CK_device_sessions_UserType",
                    "(\"UserId\" IS NOT NULL AND \"BusinessUserId\" IS NULL) OR (\"UserId\" IS NULL AND \"BusinessUserId\" IS NOT NULL)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionHash).IsUnique();
            entity.HasIndex(e => new { e.ExpiresAt, e.RevokedAt })
                .HasFilter("\"RevokedAt\" IS NULL")
                .HasDatabaseName("IX_device_sessions_Active");
            entity.Property(e => e.SessionHash).HasMaxLength(128);
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(256);
            entity.Property(e => e.DeviceName).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.BusinessUser).WithMany().HasForeignKey(e => e.BusinessUserId)
                .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<EventTicketType>(entity =>
        {
            entity.ToTable("event_ticket_types", t =>
            {
                t.HasCheckConstraint("CK_event_ticket_types_PriceCents",
                    "\"PriceCents\" >= 0");
                t.HasCheckConstraint("CK_event_ticket_types_MaxQuantity",
                    "\"MaxQuantity\" IS NULL OR \"MaxQuantity\" > 0");
                t.HasCheckConstraint("CK_event_ticket_types_SortOrder",
                    "\"SortOrder\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EventId, e.Label });
            entity.HasIndex(e => new { e.EventId, e.SortOrder });
            entity.Property(e => e.Label).HasMaxLength(128);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Instance/child entities ──────���──────────────────────

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
            entity.HasOne(e => e.BusinessUser).WithMany().HasForeignKey(e => e.BusinessUserId)
                .OnDelete(DeleteBehavior.Restrict);
#pragma warning disable CS8603
            entity.HasGeneratedTsVectorColumn(e => e.SearchVector, "english", e => new { e.Title, Description = e.Description! })
                  .HasIndex(e => e.SearchVector).HasMethod("GIN");
#pragma warning restore CS8603
        });

        modelBuilder.Entity<BusinessUserEvent>(entity =>
        {
            entity.ToTable("business_user_events");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BusinessUserId, e.EventId }).IsUnique();
            entity.HasIndex(e => e.EventId);
            entity.HasOne(e => e.BusinessUser).WithMany().HasForeignKey(e => e.BusinessUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AssignedByBusinessUser).WithMany()
                .HasForeignKey(e => e.AssignedByBusinessUserId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases", t =>
            {
                t.HasCheckConstraint("CK_purchases_Status",
                    "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                t.HasCheckConstraint("CK_purchases_SubtotalCents",
                    "\"SubtotalCents\" >= 0");
                t.HasCheckConstraint("CK_purchases_FeeCents",
                    "\"FeeCents\" >= 0");
                t.HasCheckConstraint("CK_purchases_TotalCents",
                    "\"TotalCents\" >= 0");
                t.HasCheckConstraint("CK_purchases_TotalFormula",
                    "\"TotalCents\" = \"SubtotalCents\" + \"FeeCents\"");
                t.HasCheckConstraint("CK_purchases_SeatsReserved",
                    "\"SeatsReserved\" IS NULL OR \"SeatsReserved\" > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PurchaseNumber).IsUnique();
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("\"QrToken\" IS NOT NULL");
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.EventId, e.Status });
            entity.Property(e => e.PurchaseNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Table).WithMany().HasForeignKey(e => e.TableId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.EventTicketType).WithMany().HasForeignKey(e => e.EventTicketTypeId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PurchaseTicket>(entity =>
        {
            entity.ToTable("purchase_tickets", t =>
            {
                t.HasCheckConstraint("CK_purchase_tickets_Status",
                    "\"Status\" IN ('Unassigned','Invited','Claimed','CheckedIn')");
                t.HasCheckConstraint("CK_purchase_tickets_SeatNumber",
                    "\"SeatNumber\" > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QrToken).IsUnique();
            entity.HasIndex(e => e.InviteTokenHash).IsUnique()
                .HasFilter("\"InviteTokenHash\" IS NOT NULL");
            entity.HasIndex(e => new { e.PurchaseId, e.SeatNumber }).IsUnique();
            entity.HasIndex(e => e.GuestUserId);
            entity.Property(e => e.TicketCode).HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.InviteTokenHash).HasMaxLength(128);
            entity.Property(e => e.InvitedEmail).HasMaxLength(256);
            entity.HasOne(e => e.Purchase).WithMany(b => b.Tickets)
                .HasForeignKey(e => e.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GuestUser).WithMany()
                .HasForeignKey(e => e.GuestUserId).IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PurchaseTable>(entity =>
        {
            entity.ToTable("purchase_tables");
            entity.HasKey(e => new { e.PurchaseId, e.TableId });
            entity.HasIndex(e => e.TableId);
            entity.HasOne(e => e.Purchase).WithMany()
                .HasForeignKey(e => e.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Table).WithMany()
                .HasForeignKey(e => e.TableId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StripeTransaction>(entity =>
        {
            entity.ToTable("stripe_transactions", t =>
            {
                t.HasCheckConstraint("CK_stripe_transactions_Status",
                    "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                t.HasCheckConstraint("CK_stripe_transactions_AmountCents",
                    "\"AmountCents\" >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_Currency",
                    "\"Currency\" IN ('usd')");
                t.HasCheckConstraint("CK_stripe_transactions_RefundLifecycle",
                    "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");
                t.HasCheckConstraint("CK_stripe_transactions_PaidLifecycle",
                    "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");
                t.HasCheckConstraint("CK_stripe_transactions_PendingNoPaidDate",
                    "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");
                t.HasCheckConstraint("CK_stripe_transactions_NotRefundedNoRefundDate",
                    "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");
                t.HasCheckConstraint("CK_stripe_transactions_TransferAmount",
                    "\"TransferAmountCents\" IS NULL OR \"TransferAmountCents\" >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_TaxAmount",
                    "\"TaxAmountCents\" IS NULL OR \"TaxAmountCents\" >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_StripeFees",
                    "\"StripeFeesCents\" IS NULL OR \"StripeFeesCents\" >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_TotalCharged",
                    "\"TotalChargedCents\" IS NULL OR \"TotalChargedCents\" >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentIntentId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.PaidAt });
            entity.Property(e => e.PaymentIntentId).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.RefundId).HasMaxLength(128);
            entity.Property(e => e.TaxCalculationId).HasMaxLength(128);
            entity.Property(e => e.TaxTransactionId).HasMaxLength(128);
            entity.HasOne(e => e.Purchase).WithOne(b => b.StripeTransaction)
                .HasForeignKey<StripeTransaction>(e => e.PurchaseId)
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
            entity.Property(e => e.UploaderType).HasMaxLength(10);
            entity.Property(e => e.AltText).HasMaxLength(512);
            entity.Property(e => e.Caption).HasMaxLength(1024);
            entity.Property(e => e.ContentType).HasMaxLength(64);
            entity.Property(e => e.Checksum).HasMaxLength(128);
            entity.Property(e => e.Tag).HasMaxLength(50).HasDefaultValue("Generic");
        });

        modelBuilder.Entity<VenueImage>(entity =>
        {
            entity.ToTable("venue_images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VenueId, e.ImageId }).IsUnique();
            entity.HasIndex(e => new { e.VenueId, e.SortOrder });
            entity.HasIndex(e => e.VenueId)
                .IsUnique()
                .HasFilter("\"IsPrimary\" = true")
                .HasDatabaseName("IX_venue_images_VenueId_PrimaryUnique");
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlatformImage>(entity =>
        {
            entity.ToTable("platform_images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ImageId).IsUnique();
            entity.HasIndex(e => e.SortOrder);
            entity.HasIndex(e => e.Tag);
            entity.Property(e => e.Tag).HasMaxLength(64);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventImage>(entity =>
        {
            entity.ToTable("event_images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EventId, e.ImageId }).IsUnique();
            entity.HasIndex(e => new { e.EventId, e.SortOrder });
            entity.HasIndex(e => e.EventId)
                .IsUnique()
                .HasFilter("\"IsPrimary\" = true")
                .HasDatabaseName("IX_event_images_EventId_PrimaryUnique");
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImageId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<BusinessLog>(entity =>
        {
            entity.ToTable("business_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Action);
            entity.Property(e => e.Action).HasMaxLength(128);
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

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs", t =>
            {
                t.HasCheckConstraint("CK_audit_logs_ActorType",
                    "\"ActorType\" IN ('User','Admin','Developer','System')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.EventType).HasMaxLength(128);
            entity.Property(e => e.Action).HasMaxLength(128);
            entity.Property(e => e.SubjectType).HasMaxLength(64);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.Ip).HasMaxLength(45);
            entity.HasIndex(e => new { e.ActorType, e.ActorId, e.CreatedAt })
                .HasDatabaseName("idx_audit_logs_actor");
            entity.HasIndex(e => new { e.SubjectType, e.SubjectId, e.CreatedAt })
                .HasDatabaseName("idx_audit_logs_subject");
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
            entity.Property(e => e.Diagnostics).HasColumnType("jsonb");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Type);
        });

        // ─── Read-only views ─────────────────────────────────────

        modelBuilder.Entity<EventView>(entity =>
        {
            entity.ToView("v_events");
            entity.HasKey(e => e.EventId);
        });

        modelBuilder.Entity<EventSummaryView>(entity =>
        {
            entity.ToView("v_event_summary");
            entity.HasKey(e => e.EventId);
        });

        modelBuilder.Entity<TableView>(entity =>
        {
            entity.ToView("v_tables");
            entity.HasKey(e => e.TableId);
        });

        modelBuilder.Entity<PurchaseView>(entity =>
        {
            entity.ToView("v_purchases");
            entity.HasKey(e => e.PurchaseId);
        });

        modelBuilder.Entity<PurchaseTicketView>(entity =>
        {
            entity.ToView("v_purchase_tickets");
            entity.HasKey(e => e.PurchaseTicketId);
        });

        modelBuilder.Entity<VenueView>(entity =>
        {
            entity.ToView("v_venues");
            entity.HasKey(e => e.VenueId);
        });

        modelBuilder.Entity<UserProfileView>(entity =>
        {
            entity.ToView("v_user_profile");
            entity.HasKey(e => e.UserId);
        });

        modelBuilder.Entity<EventTablesSummaryView>(entity =>
        {
            entity.ToView("v_event_tables_summary");
            entity.HasKey(e => e.EventTableId);
        });

        modelBuilder.Entity<EventTicketTypeSummaryView>(entity =>
        {
            entity.ToView("v_event_ticket_types_summary");
            entity.HasKey(e => e.EventTicketTypeId);
        });

        modelBuilder.Entity<BusinessUserView>(entity =>
        {
            entity.ToView("v_business_users");
            entity.HasKey(e => e.BusinessUserId);
            entity.Property(e => e.Role).HasConversion<string>();
        });

        modelBuilder.Entity<BusinessUserEventView>(entity =>
        {
            entity.ToView("v_business_user_events");
            entity.HasKey(e => e.BusinessUserEventId);
        });

        modelBuilder.Entity<DeviceSessionView>(entity =>
        {
            entity.ToView("v_device_sessions");
            entity.HasKey(e => e.DeviceSessionId);
        });

        modelBuilder.Entity<InvitationView>(entity =>
        {
            entity.ToView("v_invitations");
            entity.HasKey(e => e.InvitationId);
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<FeedbackView>(entity =>
        {
            entity.ToView("v_feedbacks");
            entity.HasKey(e => e.FeedbackId);
        });

        modelBuilder.Entity<EventImageView>(entity =>
        {
            entity.ToView("v_event_images");
            entity.HasKey(e => e.EventImageId);
        });

        modelBuilder.Entity<VenueImageView>(entity =>
        {
            entity.ToView("v_venue_images");
            entity.HasKey(e => e.VenueImageId);
        });

        modelBuilder.Entity<PlatformImageView>(entity =>
        {
            entity.ToView("v_platform_images");
            entity.HasKey(e => e.PlatformImageId);
        });

        modelBuilder.Entity<BusinessLogView>(entity =>
        {
            entity.ToView("v_business_logs");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SystemLogView>(entity =>
        {
            entity.ToView("v_system_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(30);
        });
    }
}
