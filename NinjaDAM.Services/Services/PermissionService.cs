using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NinjaDAM.DTO.Permission;
using NinjaDAM.Entity.Data;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IRepository<PermissionDetail> _permissionRepo;
        private readonly IRepository<UserPermission> _userPermissionRepo;
        private readonly IRepository<RolePermission> _rolePermissionRepo;
        private readonly IRepository<GroupPermission> _groupPermissionRepo;
        private readonly IRepository<UserGroup> _userGroupRepo;
        private readonly IMapper _mapper;

        public PermissionService(
            IRepository<PermissionDetail> permissionRepo,
            IRepository<UserPermission> userPermissionRepo,
            IRepository<RolePermission> rolePermissionRepo,
            IRepository<GroupPermission> groupPermissionRepo,
            IRepository<UserGroup> userGroupRepo,
            IMapper mapper)
        {
            _permissionRepo = permissionRepo;
            _userPermissionRepo = userPermissionRepo;
            _rolePermissionRepo = rolePermissionRepo;
            _groupPermissionRepo = groupPermissionRepo;
            _userGroupRepo = userGroupRepo;
            _mapper = mapper;
        }

        public async Task<IEnumerable<PermissionDetailDto>> GetAllPermissionsAsync()
        {
            var permissions = await _permissionRepo.Query()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.PermissionName)
                .ToListAsync();

            return _mapper.Map<IEnumerable<PermissionDetailDto>>(permissions);
        }

        public async Task<PermissionDetailDto> GetPermissionByIdAsync(Guid id)
        {
            var permission = await _permissionRepo.Query()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (permission == null)
            {
                throw new KeyNotFoundException($"Permission with ID {id} not found.");
            }

            return _mapper.Map<PermissionDetailDto>(permission);
        }

        public async Task<PermissionDetailDto> CreatePermissionAsync(CreatePermissionDetailDto createDto)
        {
            var permission = _mapper.Map<PermissionDetail>(createDto);
            
            permission.CreatedAt = DateTime.UtcNow;
            permission.UpdatedAt = DateTime.UtcNow;
            permission.IsDeleted = false;

            await _permissionRepo.AddAsync(permission);
            await _permissionRepo.SaveAsync();

            return _mapper.Map<PermissionDetailDto>(permission);
        }

        public async Task<PermissionDetailDto> UpdatePermissionAsync(Guid id, UpdatePermissionDetailDto updateDto)
        {
            var permission = await _permissionRepo.Query()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (permission == null)
            {
                throw new KeyNotFoundException($"Permission with ID {id} not found.");
            }

            if (updateDto.PermissionName != null) permission.PermissionName = updateDto.PermissionName;
            if (updateDto.IsActive.HasValue) permission.IsActive = updateDto.IsActive.Value;
            if (updateDto.IsDeleted.HasValue) permission.IsDeleted = updateDto.IsDeleted.Value;
            if (updateDto.ByDefault != null) permission.ByDefault = updateDto.ByDefault;

            permission.UpdatedAt = DateTime.UtcNow;

            _permissionRepo.Update(permission);
            await _permissionRepo.SaveAsync();

            return _mapper.Map<PermissionDetailDto>(permission);
        }

        public async Task DeletePermissionAsync(Guid id)
        {
            var permission = await _permissionRepo.Query()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (permission == null || permission.IsDeleted)
            {
                 throw new KeyNotFoundException($"Permission with ID {id} not found.");
            }

            permission.IsDeleted = true;
            permission.UpdatedAt = DateTime.UtcNow;

            _permissionRepo.Update(permission);
            await _permissionRepo.SaveAsync();
        }

        public async Task AssignPermissionsToUserAsync(string userId, List<Guid> permissionIds)
        {
            // 1. Get existing permissions for user
            var existingPermissions = await _userPermissionRepo.Query()
                .Where(up => up.UserId == userId)
                .ToListAsync();

            // 2. Determine permissions to add and remove
            var existingIds = existingPermissions.Select(up => up.PermissionDetailId).ToList();
            
            var toAdd = permissionIds.Except(existingIds).ToList();
            var toRemove = existingIds.Except(permissionIds).ToList();

            // 3. Remove
            if (toRemove.Any())
            {
                var permissionsToRemove = existingPermissions.Where(up => toRemove.Contains(up.PermissionDetailId)).ToList();
                foreach (var perm in permissionsToRemove)
                {
                    _userPermissionRepo.Delete(perm);
                }
            }

            // 4. Add
            if (toAdd.Any())
            {
                foreach (var permId in toAdd)
                {
                    await _userPermissionRepo.AddAsync(new UserPermission
                    {
                        UserId = userId,
                        PermissionDetailId = permId
                    });
                }
            }

            await _userPermissionRepo.SaveAsync();
        }

        public async Task AssignDefaultPermissionsAsync(string userId, string role)
        {
            var permissionsQuery = _permissionRepo.Query().Where(p => !p.IsDeleted && p.IsActive);
            List<PermissionDetail> permissionsToAssign;

            if (role == "Admin")
            {
                // Admins get all active permissions
                permissionsToAssign = await permissionsQuery.ToListAsync();
            }
            else if (role == "Editor")
            {
                // Editors get permissions marked for Editor or Viewer
                permissionsToAssign = await permissionsQuery
                    .Where(p => p.ByDefault == "Editor" || p.ByDefault == "Viewer")
                    .ToListAsync();
            }
            else if (role == "Viewer")
            {
                // Viewers get permissions marked for Viewer
                permissionsToAssign = await permissionsQuery
                    .Where(p => p.ByDefault == "Viewer")
                    .ToListAsync();
            }
            else
            {
                permissionsToAssign = new List<PermissionDetail>();
            }

            if (permissionsToAssign.Any())
            {
                foreach (var perm in permissionsToAssign)
                {
                    await _userPermissionRepo.AddAsync(new UserPermission
                    {
                        UserId = userId,
                        PermissionDetailId = perm.Id
                    });
                }
                await _userPermissionRepo.SaveAsync();
            }
        }

        public async Task<IEnumerable<PermissionDetailDto>> GetPermissionsByUserIdAsync(string userId)
        {
            // 1. Get explicit user permissions
            var userPermissions = await _userPermissionRepo.Query()
                .Include(up => up.PermissionDetail)
                .Where(up => up.UserId == userId && !up.PermissionDetail.IsDeleted && up.PermissionDetail.IsActive)
                .Select(up => up.PermissionDetail)
                .ToListAsync();

            // 2. Get group permissions
            var groupIds = await _userGroupRepo.Query()
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.GroupId)
                .ToListAsync();

            var groupPermissions = await _groupPermissionRepo.Query()
                .Include(gp => gp.PermissionDetail)
                .Where(gp => groupIds.Contains(gp.GroupId) && !gp.PermissionDetail.IsDeleted && gp.PermissionDetail.IsActive)
                .Select(gp => gp.PermissionDetail)
                .ToListAsync();

            // 3. Merge and distinct
            var allPermissions = userPermissions.Concat(groupPermissions)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            return _mapper.Map<IEnumerable<PermissionDetailDto>>(allPermissions);
        }

        public async Task AssignPermissionsToRoleAsync(string roleId, List<Guid> permissionIds)
        {
            // Try to resolve name to ID if needed
            var finalRoleId = roleId;
            var roleExists = await _rolePermissionRepo.Query().AnyAsync(rp => rp.RoleId == roleId);
            if (!roleExists)
            {
                // This is a bit hacky since we don't have a RoleRepo here, but we can access the context if needed.
                // However, let's assume for now the client might send the name.
                // If it's a new assignment, roleExists will be false anyway.
            }

            // 1. Get existing permissions for role
            var existingPermissions = await _rolePermissionRepo.Query()
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            // 2. Determine permissions to add and remove
            var existingIds = existingPermissions.Select(rp => rp.PermissionDetailId).ToList();
            
            var toAdd = permissionIds.Except(existingIds).ToList();
            var toRemove = existingIds.Except(permissionIds).ToList();

            // 3. Remove
            if (toRemove.Any())
            {
                var permissionsToRemove = existingPermissions.Where(rp => toRemove.Contains(rp.PermissionDetailId)).ToList();
                foreach (var perm in permissionsToRemove)
                {
                    _rolePermissionRepo.Delete(perm);
                }
            }

            // 4. Add
            if (toAdd.Any())
            {
                foreach (var permId in toAdd)
                {
                    await _rolePermissionRepo.AddAsync(new RolePermission
                    {
                        RoleId = roleId,
                        PermissionDetailId = permId
                    });
                }
            }

            await _rolePermissionRepo.SaveAsync();
        }

        public async Task<IEnumerable<PermissionDetailDto>> GetPermissionsByRoleIdAsync(string roleId)
        {
            var rolePermissions = await _rolePermissionRepo.Query()
                .Include(rp => rp.PermissionDetail)
                .Where(rp => rp.RoleId == roleId && !rp.PermissionDetail.IsDeleted && rp.PermissionDetail.IsActive)
                .Select(rp => rp.PermissionDetail)
                .ToListAsync();

            return _mapper.Map<IEnumerable<PermissionDetailDto>>(rolePermissions);
        }
    }
}
