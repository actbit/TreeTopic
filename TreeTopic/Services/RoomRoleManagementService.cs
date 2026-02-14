using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;

namespace TreeTopic.Services;

public class RoomRoleManagementService : BaseService
{
    private readonly IRoomRoleRepository _roleRepository;
    private readonly RoomRoleManager _roleManager;
    private readonly ILogger<RoomRoleManagementService> _logger;

    public RoomRoleManagementService(
        IRoomRoleRepository roleRepository,
        RoomRoleManager roleManager,
        ILogger<RoomRoleManagementService> logger) : base(logger)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<Result<List<RoomRole>>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var roles = await _roleRepository.ListAsync(cancellationToken);
            return Result<List<RoomRole>>.Success(roles);
        }, nameof(ListRolesAsync));
    }

    public async Task<Result<RoomRole>> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (id == Guid.Empty)
            {
                return Result<RoomRole>.BadRequest("Role ID cannot be empty");
            }

            var role = await _roleRepository.FindByIdAsync(id, cancellationToken);
            if (role == null)
            {
                return Result<RoomRole>.NotFound($"Role '{id}' not found");
            }

            return Result<RoomRole>.Success(role);
        }, nameof(GetRoleByIdAsync));
    }

    public async Task<Result<RoomRole>> CreateRoleAsync(CreateRoomRoleRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var cleanName = request.Name!.Trim();

            // Check if role already exists
            if (await _roleRepository.ExistsAsync(cleanName, cancellationToken))
            {
                return Result<RoomRole>.Conflict($"Role '{cleanName}' already exists");
            }

            var role = new RoomRole
            {
                Name = cleanName,
                Description = request.Description?.Trim(),
                SortOrder = request.SortOrder,
                Permissions = request.Permissions.Select(p => new RoomRolePermission
                {
                    Id = Guid.CreateVersion7(),
                    PermissionName = p
                }).ToList()
            };

            var createdRole = await _roleManager.CreateAsync(role, cancellationToken);
            _logger.LogInformation("RoomRole created: {Name} ({Id})", createdRole.Name, createdRole.Id);

            return Result<RoomRole>.Success(createdRole, 201);
        }, nameof(CreateRoleAsync));
    }

    public async Task<Result<RoomRole>> UpdateRoleAsync(Guid id, UpdateRoomRoleRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (id == Guid.Empty)
            {
                return Result<RoomRole>.BadRequest("Role ID cannot be empty");
            }

            var role = await _roleRepository.FindByIdAsync(id, cancellationToken);
            if (role == null)
            {
                return Result<RoomRole>.NotFound($"Role '{id}' not found");
            }

            var cleanName = request.Name.Trim();

            // Check if another role with the same name exists
            var existingRole = await _roleRepository.FindByNameAsync(cleanName, cancellationToken);
            if (existingRole != null && existingRole.Id != id)
            {
                return Result<RoomRole>.Conflict($"Role '{cleanName}' already exists");
            }

            role.Name = cleanName;
            role.Description = request.Description?.Trim();
            role.SortOrder = request.SortOrder;

            // Update permissions
            role.Permissions.Clear();
            foreach (var permName in request.Permissions)
            {
                role.Permissions.Add(new RoomRolePermission
                {
                    Id = Guid.CreateVersion7(),
                    RoomRoleId = role.Id,
                    PermissionName = permName
                });
            }

            var updatedRole = await _roleRepository.UpdateAsync(role, cancellationToken);
            _logger.LogInformation("RoomRole updated: {Name} ({Id})", updatedRole.Name, updatedRole.Id);

            return Result<RoomRole>.Success(updatedRole);
        }, nameof(UpdateRoleAsync));
    }

    public async Task<Result> DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (id == Guid.Empty)
            {
                return Result.BadRequest("Role ID cannot be empty");
            }

            var deleted = await _roleRepository.DeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return Result.NotFound($"Role '{id}' not found");
            }

            _logger.LogInformation("RoomRole deleted: {Id}", id);
            return Result.NoContent();
        }, nameof(DeleteRoleAsync));
    }

    /// <summary>
    /// DTOに変換
    /// </summary>
    public static RoomRoleDto ToDto(RoomRole role)
    {
        return new RoomRoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            SortOrder = role.SortOrder,
            Permissions = role.Permissions.Select(p => p.PermissionName).ToList()
        };
    }

    /// <summary>
    /// RoomRoleをWithUsers DTOに変換
    /// </summary>
    public static RoomRoleWithUsersDto ToWithUsersDto(RoomRole role, int userCount)
    {
        return new RoomRoleWithUsersDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            SortOrder = role.SortOrder,
            Permissions = role.Permissions.Select(p => p.PermissionName).ToList(),
            UserCount = userCount
        };
    }
}
