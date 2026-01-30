using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.Group;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{
    public class GroupService : IGroupService
    {
        private readonly IRepository<Group> _groupRepo;
        private readonly IRepository<GroupPermission> _groupPermissionRepo;
        private readonly UserManager<Users> _userManager;
        private readonly IMapper _mapper;
        private readonly ILogger<GroupService> _logger;

        public GroupService(
            IRepository<Group> groupRepo,
            IRepository<GroupPermission> groupPermissionRepo,
            UserManager<Users> userManager,
            IMapper mapper,
            ILogger<GroupService> logger)
        {
            _groupRepo = groupRepo;
            _groupPermissionRepo = groupPermissionRepo;
            _userManager = userManager;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<GroupDto>> GetAllGroupsAsync(string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) return Enumerable.Empty<GroupDto>();

            var groups = await _groupRepo.Query()
                .Where(g => g.CompanyId == requestingUser.CompanyId && !g.IsDeleted)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<GroupDto>>(groups);
        }

        public async Task<GroupDto> GetGroupByIdAsync(Guid id, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) throw new UnauthorizedAccessException();

            var group = await _groupRepo.Query()
                .FirstOrDefaultAsync(g => g.Id == id && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

            if (group == null) throw new KeyNotFoundException("Group not found");

            return _mapper.Map<GroupDto>(group);
        }

        public async Task<GroupDto> CreateGroupAsync(CreateGroupDto createDto, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) throw new UnauthorizedAccessException();

            var nameTrimmed = createDto.Name?.Trim() ?? string.Empty;
            var nameLower = nameTrimmed.ToLower();

            if (nameLower == "viewer")
            {
                throw new InvalidOperationException("The group name 'Viewer' is reserved.");
            }

            var exists = await _groupRepo.Query()
                .AnyAsync(g => g.Name.ToLower() == nameLower && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

            if (exists)
            {
                _logger.LogWarning("Attempted to create duplicate group name: {Name} for Company: {CompanyId}", nameTrimmed, requestingUser.CompanyId);
                throw new InvalidOperationException("A group with this name already exists.");
            }

            var group = _mapper.Map<Group>(createDto);
            group.Name = nameTrimmed;
            group.Id = Guid.NewGuid();
            group.CompanyId = requestingUser.CompanyId.Value;
            group.CreatedAt = DateTime.UtcNow;
            group.UpdatedAt = DateTime.UtcNow;
            group.IsDeleted = false;

            await _groupRepo.AddAsync(group);
            await _groupRepo.SaveAsync();

            return _mapper.Map<GroupDto>(group);
        }

        public async Task<GroupDto> UpdateGroupAsync(Guid id, UpdateGroupDto updateDto, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) throw new UnauthorizedAccessException();

            var group = await _groupRepo.Query()
                .FirstOrDefaultAsync(g => g.Id == id && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

            if (group == null) throw new KeyNotFoundException("Group not found");

            if (!string.IsNullOrWhiteSpace(updateDto.Name))
            {
                var nameTrimmed = updateDto.Name.Trim();
                var nameLower = nameTrimmed.ToLower();

                if (nameLower == "viewer")
                {
                    throw new InvalidOperationException("The group name 'Viewer' is reserved.");
                }

                var exists = await _groupRepo.Query()
                    .AnyAsync(g => g.Id != id && g.Name.ToLower() == nameLower && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

                if (exists)
                {
                    _logger.LogWarning("Attempted to update to duplicate group name: {Name} for Company: {CompanyId}", nameTrimmed, requestingUser.CompanyId);
                    throw new InvalidOperationException("A group with this name already exists.");
                }

                group.Name = nameTrimmed;
            }

            if (updateDto.IsActive.HasValue) group.IsActive = updateDto.IsActive.Value;

            group.UpdatedAt = DateTime.UtcNow;

            _groupRepo.Update(group);
            await _groupRepo.SaveAsync();

            return _mapper.Map<GroupDto>(group);
        }

        public async Task<bool> DeleteGroupAsync(Guid id, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) throw new UnauthorizedAccessException();

            var group = await _groupRepo.Query()
                .FirstOrDefaultAsync(g => g.Id == id && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

            if (group == null) return false;

            group.IsDeleted = true;
            group.UpdatedAt = DateTime.UtcNow;

            _groupRepo.Update(group);
            await _groupRepo.SaveAsync();

            return true;
        }

        public async Task AssignPermissionsToGroupAsync(Guid groupId, List<Guid> permissionIds, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) throw new UnauthorizedAccessException();

            var group = await _groupRepo.Query()
                .FirstOrDefaultAsync(g => g.Id == groupId && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

            if (group == null) throw new KeyNotFoundException("Group not found");

            // Remove existing permissions
            var existing = await _groupPermissionRepo.Query()
                .Where(gp => gp.GroupId == groupId)
                .ToListAsync();

            foreach (var item in existing)
            {
                _groupPermissionRepo.Delete(item);
            }

            // Add new permissions
            foreach (var permId in permissionIds)
            {
                await _groupPermissionRepo.AddAsync(new GroupPermission
                {
                    GroupId = groupId,
                    PermissionDetailId = permId
                });
            }

            await _groupPermissionRepo.SaveAsync();
        }

        public async Task<IEnumerable<Guid>> GetPermissionsByGroupIdAsync(Guid groupId, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null || requestingUser.CompanyId == null) throw new UnauthorizedAccessException();

            var group = await _groupRepo.Query()
                .FirstOrDefaultAsync(g => g.Id == groupId && g.CompanyId == requestingUser.CompanyId && !g.IsDeleted);

            if (group == null) throw new KeyNotFoundException("Group not found");

            var permissions = await _groupPermissionRepo.Query()
                .Where(gp => gp.GroupId == groupId)
                .Select(gp => gp.PermissionDetailId)
                .ToListAsync();

            return permissions;
        }
    }
}
