using Microsoft.EntityFrameworkCore;
using SmartFleet.Models;

namespace SmartFleet.Data.Repositories;

public class UserRepository(ApplicationDbContext context) : IUserRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IReadOnlyCollection<User>> GetAllAsync(bool includeVehicle, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();
        if (includeVehicle)
        {
            query = query.Include(u => u.Vehicle);
        }

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByIdAsync(int id, bool includeVehicle, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();

        if (includeVehicle)
        {
            query = query.Include(u => u.Vehicle);
        }

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, bool includeVehicle, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();

        if (includeVehicle)
        {
            query = query.Include(u => u.Vehicle);
        }

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var idSet = ids.ToHashSet();
        return await _context.Users
            .Where(u => idSet.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<User?> GetFirstAvailableDriverAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .Where(u => u.Role == UserRole.Driver && u.VehicleId == null)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(User user) => _context.Users.Add(user);

    public void Update(User user) => _context.Users.Update(user);

    public void Remove(User user) => _context.Users.Remove(user);

    public void Attach(User user)
    {
        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Attach(user);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
