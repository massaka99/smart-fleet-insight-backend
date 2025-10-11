using Microsoft.EntityFrameworkCore;
using SmartFleet.Models;
using SmartFleet.Models.Chat;

namespace SmartFleet.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<VehicleState> VehicleStates => Set<VehicleState>();
    public DbSet<VehicleTelemetryRawMessage> VehicleTelemetryRawMessages => Set<VehicleTelemetryRawMessage>();
    public DbSet<VehicleTelemetryDeadLetter> VehicleTelemetryDeadLetters => Set<VehicleTelemetryDeadLetter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.Property(v => v.LicensePlate).IsRequired().HasMaxLength(15);
            entity.Property(v => v.VehicleType).IsRequired().HasMaxLength(100);
            entity.Property(v => v.FuelType).IsRequired().HasMaxLength(50);
            entity.Property(v => v.BodyType).HasMaxLength(100);

            entity.HasOne(v => v.Driver)
                .WithMany()
                .HasForeignKey(v => v.DriverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VehicleState>(entity =>
        {
            entity.HasIndex(t => t.VehicleId).IsUnique();
            entity.HasIndex(t => t.NumberPlate);
            entity.HasIndex(t => t.VehicleCode);

            entity.Property(t => t.TimestampUtc).HasColumnType("timestamp with time zone");
            entity.Property(t => t.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(t => t.Vehicle)
                .WithMany()
                .HasForeignKey(t => t.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleTelemetryRawMessage>(entity =>
        {
            entity.HasIndex(r => r.TelemetryId);
            entity.Property(r => r.PayloadJson).HasColumnType("jsonb");
            entity.Property(r => r.ReceivedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<VehicleTelemetryDeadLetter>(entity =>
        {
            entity.Property(d => d.Reason).HasMaxLength(256);
            entity.Property(d => d.PayloadJson).HasColumnType("jsonb");
            entity.Property(d => d.CreatedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<ChatThread>(entity =>
        {
            entity.HasIndex(t => new { t.ParticipantAId, t.ParticipantBId }).IsUnique();

            entity.HasOne(t => t.ParticipantA)
                .WithMany()
                .HasForeignKey(t => t.ParticipantAId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ParticipantB)
                .WithMany()
                .HasForeignKey(t => t.ParticipantBId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.Property(m => m.Body).IsRequired().HasMaxLength(4000);
            entity.Property(m => m.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property(m => m.FailureReason).HasMaxLength(1024);

            entity.HasOne(m => m.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => m.ThreadId);
            entity.HasIndex(m => m.SenderId);
            entity.HasIndex(m => m.RecipientId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).IsRequired();
            entity.Property(u => u.LastName).IsRequired();
            entity.Property(u => u.Email).IsRequired();
        });

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "Default",
                LastName = "Admin",
                Email = "admin@smartfleet.local",
                ProfileImageUrl = null,
                Age = 35,
                Role = UserRole.Admin,
                PasswordHash = "AQAAAAIAAYagAAAAEOCJVUh+L5Pygby4OYlYZtLtrN/TrziEgkz6euPJtg4/c5uRcY1y6TrWBJF3+rt5Cg=="
            });
    }
}
