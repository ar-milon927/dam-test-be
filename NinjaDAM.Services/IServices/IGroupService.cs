using NinjaDAM.DTO.Group;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NinjaDAM.Services.IServices
{
    public interface IGroupService
    {
        Task<IEnumerable<GroupDto>> GetAllGroupsAsync(string requestingUserId);
        Task<GroupDto> GetGroupByIdAsync(Guid id, string requestingUserId);
        Task<GroupDto> CreateGroupAsync(CreateGroupDto createDto, string requestingUserId);
        Task<GroupDto> UpdateGroupAsync(Guid id, UpdateGroupDto updateDto, string requestingUserId);
        Task<bool> DeleteGroupAsync(Guid id, string requestingUserId);
        
        // Group permissions
        Task AssignPermissionsToGroupAsync(Guid groupId, List<Guid> permissionIds, string requestingUserId);
        Task<IEnumerable<Guid>> GetPermissionsByGroupIdAsync(Guid groupId, string requestingUserId);
    }
}
