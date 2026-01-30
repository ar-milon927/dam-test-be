using NinjaDAM.DTO.Permission;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NinjaDAM.Services.IServices
{
    public interface IPermissionService
    {
        Task<IEnumerable<PermissionDetailDto>> GetAllPermissionsAsync();
        Task<PermissionDetailDto> GetPermissionByIdAsync(Guid id);
        Task<PermissionDetailDto> CreatePermissionAsync(CreatePermissionDetailDto createDto);
        Task<PermissionDetailDto> UpdatePermissionAsync(Guid id, UpdatePermissionDetailDto updateDto);
        Task DeletePermissionAsync(Guid id); // Soft delete
        
        // User Assignment
        Task AssignPermissionsToUserAsync(string userId, List<Guid> permissionIds);
        Task AssignDefaultPermissionsAsync(string userId, string role);
        Task<IEnumerable<PermissionDetailDto>> GetPermissionsByUserIdAsync(string userId);

        // Role Assignment
        Task AssignPermissionsToRoleAsync(string roleId, List<Guid> permissionIds);
        Task<IEnumerable<PermissionDetailDto>> GetPermissionsByRoleIdAsync(string roleId);
    }
}
