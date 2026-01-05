using Microsoft.EntityFrameworkCore;
using TripShare.Domain.Entities;

namespace TripShare.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<SmsOtpToken> SmsOtpTokens => Set<SmsOtpToken>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripRoutePoint> TripRoutePoints => Set<TripRoutePoint>();
    public DbSet<TripSegment> TripSegments => Set<TripSegment>();

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingSegmentAllocation> BookingSegmentAllocations => Set<BookingSegmentAllocation>();

    public DbSet<Rating> Ratings => Set<Rating>();

    // Prod-ready features (+10): moderation & user safety
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<TripShareLink> TripShareLinks => Set<TripShareLink>();
    public DbSet<SafetyIncident> SafetyIncidents => Set<SafetyIncident>();
    public DbSet<IdentityVerificationRequest> IdentityVerificationRequests => Set<IdentityVerificationRequest>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => new { x.AuthProvider, x.ProviderUserId }).IsUnique();
            b.HasIndex(x => x.PhoneNumber);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.DisplayName).HasMaxLength(120);
            b.Property(x => x.PasswordHash).HasMaxLength(400);
            b.Property(x => x.PasswordSalt).HasMaxLength(200);
            b.Property(x => x.Role).HasMaxLength(32);
        });

        modelBuilder.Entity<Trip>(b =>
        {
            b.HasIndex(x => x.DriverId);
            b.HasIndex(x => new { x.Status, x.UpdatedAt });
            b.Property(x => x.Currency).HasMaxLength(8);
            b.Property(x => x.BaseCurrencyRate).HasPrecision(18, 6);
            b.Property(x => x.DefaultPricePerSeat).HasPrecision(18, 2);
            b.HasMany(x => x.RoutePoints).WithOne(x => x.Trip!).HasForeignKey(x => x.TripId);
            b.HasMany(x => x.Segments).WithOne(x => x.Trip!).HasForeignKey(x => x.TripId);
        });

        modelBuilder.Entity<TripRoutePoint>(b =>
        {
            b.HasIndex(x => new { x.TripId, x.OrderIndex }).IsUnique();
            b.Property(x => x.DisplayAddress).HasMaxLength(400);
            b.Property(x => x.PlaceId).HasMaxLength(128);
        });

        modelBuilder.Entity<TripSegment>(b =>
        {
            b.HasIndex(x => new { x.TripId, x.OrderIndex }).IsUnique();
            b.Property(x => x.Currency).HasMaxLength(8);
            b.Property(x => x.Price).HasPrecision(18, 2);
            b.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Booking>(b =>
        {
            b.HasIndex(x => new { x.TripId, x.PassengerId });
            b.HasIndex(x => new { x.TripId, x.Status, x.ProgressStatus });
            b.Property(x => x.Currency).HasMaxLength(8);
            b.Property(x => x.PickupPlaceName).HasMaxLength(200);
            b.Property(x => x.DropoffPlaceName).HasMaxLength(200);
            b.Property(x => x.PickupPlaceId).HasMaxLength(128);
            b.Property(x => x.DropoffPlaceId).HasMaxLength(128);
            b.Property(x => x.PriceTotal).HasPrecision(18, 2);
            b.HasMany(x => x.SegmentAllocations).WithOne(x => x.Booking!).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<BookingSegmentAllocation>(b =>
        {
            b.HasIndex(x => new { x.BookingId, x.TripSegmentId }).IsUnique();
        });


        modelBuilder.Entity<Vehicle>(b =>
        {
            b.HasIndex(x => x.OwnerUserId).IsUnique();
            b.Property(x => x.Make).HasMaxLength(80);
            b.Property(x => x.Model).HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(40);
            b.Property(x => x.PlateNumber).HasMaxLength(32);
            b.HasOne(x => x.Owner).WithOne(x => x.Vehicle!).HasForeignKey<Vehicle>(x => x.OwnerUserId);
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            b.Property(x => x.Title).HasMaxLength(140);
            b.Property(x => x.Body).HasMaxLength(600);
        });

        modelBuilder.Entity<UserBlock>(b =>
        {
            b.HasIndex(x => new { x.BlockerUserId, x.BlockedUserId }).IsUnique();
        });

        modelBuilder.Entity<Report>(b =>
        {
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.Property(x => x.Reason).HasMaxLength(120);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.Property(x => x.AdminNote).HasMaxLength(2000);
        });

        modelBuilder.Entity<SiteSetting>(b =>
        {
            b.HasIndex(x => x.Key).IsUnique();
            b.Property(x => x.Key).HasMaxLength(120);
            b.Property(x => x.Value).HasMaxLength(4000);
        });


        modelBuilder.Entity<Rating>(b =>
        {
            b.HasIndex(x => new { x.BookingId, x.FromUserId }).IsUnique();
        });

        modelBuilder.Entity<SmsOtpToken>(b =>
        {
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.PhoneNumber);
            b.Property(x => x.PhoneNumber).HasMaxLength(64);
        });

        modelBuilder.Entity<EmergencyContact>(b =>
        {
            b.HasIndex(x => x.UserId).IsUnique();
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.PhoneNumber).HasMaxLength(64);
            b.Property(x => x.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<MessageThread>(b =>
        {
            b.HasIndex(x => new { x.BookingId, x.TripId });
            b.HasIndex(x => new { x.ParticipantAId, x.ParticipantBId });
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.HasIndex(x => new { x.ThreadId, x.SentAt });
            b.Property(x => x.Body).HasMaxLength(2000);
            b.HasOne(x => x.Thread).WithMany(x => x.Messages).HasForeignKey(x => x.ThreadId);
        });

        modelBuilder.Entity<TripShareLink>(b =>
        {
            b.HasIndex(x => x.TripId);
            b.HasIndex(x => x.Token).IsUnique();
            b.Property(x => x.Token).HasMaxLength(120);
        });

        modelBuilder.Entity<SafetyIncident>(b =>
        {
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.Property(x => x.Summary).HasMaxLength(800);
        });

        modelBuilder.Entity<IdentityVerificationRequest>(b =>
        {
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => new { x.Status, x.SubmittedAt });
            b.Property(x => x.DocumentType).HasMaxLength(80);
            b.Property(x => x.DocumentReference).HasMaxLength(200);
            b.Property(x => x.ReviewerNote).HasMaxLength(600);
            b.Property(x => x.KycProvider).HasMaxLength(120);
            b.Property(x => x.KycReference).HasMaxLength(200);
        });

        modelBuilder.Entity<BackgroundJob>(b =>
        {
            b.HasIndex(x => new { x.Status, x.RunAfter, x.Attempts });
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Payload).HasMaxLength(4000);
        });
    }
}
