using Microsoft.EntityFrameworkCore;
using SmartFleet.Models;

namespace SmartFleet.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<User> Users => Set<User>();

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
