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

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "Default",
                LastName = "Admin",
                Age = 35,
                Role = UserRole.Admin,
                PasswordHash = "AQAAAAIAAYagAAAAEOCJVUh+L5Pygby4OYlYZtLtrN/TrziEgkz6euPJtg4/c5uRcY1y6TrWBJF3+rt5Cg=="
            });
    }
}
