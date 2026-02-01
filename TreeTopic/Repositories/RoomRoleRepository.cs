using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class RoomRoleRepository : BaseRepository<RoomRole>, IRoomRoleRepository
{
    public RoomRoleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<RoomRole>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Query()
            .Include(r => r.Permissions)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomRole?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RoomRole?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Query()
            .AnyAsync(r => r.Name == name, cancellationToken);
    }

    public async Task<RoomRole> CreateAsync(RoomRole role, CancellationToken cancellationToken = default)
    {
        return await AddAsync(role, cancellationToken);
    }

    public new async Task<RoomRole> UpdateAsync(RoomRole role, CancellationToken cancellationToken = default)
    {
        return await base.UpdateAsync(role, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await FindByIdAsync(id, cancellationToken);
        if (role == null)
        {
            return false;
        }

        Context.RoomRoles.Remove(role);
        await Context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
