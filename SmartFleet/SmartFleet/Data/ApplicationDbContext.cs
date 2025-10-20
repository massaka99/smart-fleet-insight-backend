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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.Property(v => v.ExternalId).HasMaxLength(64);
            entity.Property(v => v.LicensePlate).IsRequired().HasMaxLength(15);
            entity.Property(v => v.VehicleType).IsRequired().HasMaxLength(100);
            entity.Property(v => v.FuelType).IsRequired().HasMaxLength(50);
            entity.Property(v => v.BodyType).HasMaxLength(100);
            entity.Property(v => v.Brand).HasMaxLength(64);
            entity.Property(v => v.FuelUnit).HasMaxLength(16);
            entity.Property(v => v.Status).HasMaxLength(32);
            entity.Property(v => v.RouteId).HasMaxLength(64);
            entity.Property(v => v.RouteSummary).HasMaxLength(256);

            entity.HasIndex(v => v.ExternalId).IsUnique();

            entity.HasOne(v => v.Driver)
                .WithOne(u => u.Vehicle)
                .HasForeignKey<User>(u => u.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.HasIndex(u => u.VehicleId).IsUnique();
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
