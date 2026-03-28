using Db.Entities;
using Db.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Db;

public class EventPlatformDbContext(
    DbContextOptions<EventPlatformDbContext> options,
    ChangeTrackingInterceptor changeTrackingInterceptor
) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<DeveloperLog> DeveloperLogs => Set<DeveloperLog>();
    public DbSet<AdminLog> AdminLogs => Set<AdminLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<TableType> TableTypes => Set<TableType>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(changeTrackingInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmailHash).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailHash).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
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

        modelBuilder.Entity<DeveloperLog>(entity =>
        {
            entity.ToTable("developer_logs");
            entity.HasKey(e => e.Id);
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
            entity.ToTable("system_logs");
            entity.HasKey(e => e.Id);
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
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Recipient).HasMaxLength(256);
            entity.Property(e => e.Subject).HasMaxLength(512);
            entity.Property(e => e.Status).HasMaxLength(20);
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

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.City);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Address).HasMaxLength(512);
            entity.Property(e => e.City).HasMaxLength(128);
            entity.Property(e => e.State).HasMaxLength(2);
            entity.Property(e => e.ZipCode).HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(4096);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Website).HasMaxLength(512);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.StartDate);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.Slug).HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(8192);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.HasOne(e => e.Venue).WithMany(v => v.Events).HasForeignKey(e => e.VenueId);
            entity.HasOne(e => e.Organizer).WithMany().HasForeignKey(e => e.OrganizerId);
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.ToTable("ticket_types");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.HasOne(e => e.Event).WithMany(ev => ev.TicketTypes).HasForeignKey(e => e.EventId);
        });

        modelBuilder.Entity<TableType>(entity =>
        {
            entity.ToTable("table_types");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Shape).HasMaxLength(20);
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenueId);
        });

        modelBuilder.Entity<Table>(entity =>
        {
            entity.ToTable("tables");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.HasOne(e => e.TableType).WithMany(tt => tt.Tables).HasForeignKey(e => e.TableTypeId);
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenueId);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.ToTable("seats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.HasOne(e => e.Table).WithMany(t => t.Seats).HasForeignKey(e => e.TableId);
        });

        modelBuilder.Entity<SeatHold>(entity =>
        {
            entity.ToTable("seat_holds");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SeatId, e.EventId, e.IsActive });
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasOne(e => e.Seat).WithMany(s => s.Holds).HasForeignKey(e => e.SeatId);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.TicketType).WithMany().HasForeignKey(e => e.TicketTypeId);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BookingNumber).IsUnique();
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("\"QrToken\" IS NOT NULL");
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.BookingNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.Notes).HasMaxLength(1024);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId);
        });

        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.ToTable("booking_items");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Booking).WithMany(b => b.Items).HasForeignKey(e => e.BookingId);
            entity.HasOne(e => e.TicketType).WithMany().HasForeignKey(e => e.TicketTypeId);
            entity.HasOne(e => e.Seat).WithMany().HasForeignKey(e => e.SeatId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentIntentId).IsUnique();
            entity.Property(e => e.PaymentIntentId).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.RefundId).HasMaxLength(128);
            entity.HasOne(e => e.Booking).WithOne(b => b.Payment).HasForeignKey<Payment>(e => e.BookingId);
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.ToTable("pricing_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId);
        });
    }
}
