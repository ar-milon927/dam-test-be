using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.UserManagement;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;
        private readonly ILogger<UserManagementService> _logger;
        private readonly IEmailService _emailService;
        private readonly IPermissionService _permissionService;
        private readonly IRepository<UserGroup> _userGroupRepo;
        private readonly IRepository<Group> _groupRepo;

        public UserManagementService(
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            IMapper mapper,
            ILogger<UserManagementService> logger,
            IEmailService emailService,
            IPermissionService permissionService,
            IRepository<UserGroup> userGroupRepo,
            IRepository<Group> groupRepo)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
            _logger = logger;
            _emailService = emailService;
            _permissionService = permissionService;
            _userGroupRepo = userGroupRepo;
            _groupRepo = groupRepo;
        }

        public async Task<List<UserResponseDto>> GetAllUsersAsync(string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null)
            {
                _logger.LogWarning("Requesting user {UserId} not found", requestingUserId);
                return new List<UserResponseDto>();
            }

            var requestingUserRoles = await _userManager.GetRolesAsync(requestingUser);
            var isSuperAdmin = requestingUserRoles.Contains("SuperAdmin");

            IQueryable<Users> usersQuery;
            if (isSuperAdmin)
            {
                usersQuery = _userManager.Users;
            }
            else
            {
                if (requestingUser.CompanyId == null)
                {
                    _logger.LogWarning("Requesting user {UserId} has no company association", requestingUserId);
                    return new List<UserResponseDto>();
                }

                // Get requesting user's group
                var requestingUserGroup = await _userGroupRepo.Query().FirstOrDefaultAsync(ug => ug.UserId == requestingUser.Id);
                
                if (requestingUserGroup != null)
                {
                    // Filter users by company AND group members
                    var userIdsInGroup = await _userGroupRepo.Query()
                        .Where(ug => ug.GroupId == requestingUserGroup.GroupId)
                        .Select(ug => ug.UserId)
                        .ToListAsync();

                    usersQuery = _userManager.Users.Where(u => u.CompanyId == requestingUser.CompanyId && userIdsInGroup.Contains(u.Id));
                }
                else
                {
                    // Fallback to company-wide if no group, or maybe restrict? Let's stay with company for now unless user specifies.
                    usersQuery = _userManager.Users.Where(u => u.CompanyId == requestingUser.CompanyId);
                }
            }

            var users = await usersQuery.ToListAsync();

            var userDtos = new List<UserResponseDto>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userDto = _mapper.Map<UserResponseDto>(user);
                userDto.Role = roles.FirstOrDefault() ?? "User";
                
                var userGroup = await _userGroupRepo.Query().FirstOrDefaultAsync(ug => ug.UserId == user.Id);
                if (userGroup != null)
                {
                    userDto.GroupId = userGroup.GroupId;
                    var group = await _groupRepo.GetByIdAsync(userGroup.GroupId);
                    userDto.GroupName = group?.Name;
                }

                userDtos.Add(userDto);
            }

            return userDtos.OrderBy(u => u.Email).ToList();
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(string userId, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null)
            {
                _logger.LogWarning("Requesting user {UserId} not found", requestingUserId);
                return null;
            }

            var requestingUserRoles = await _userManager.GetRolesAsync(requestingUser);
            var isSuperAdmin = requestingUserRoles.Contains("SuperAdmin");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            if (!isSuperAdmin && user.CompanyId != requestingUser.CompanyId)
            {
                _logger.LogWarning("User {UserId} access denied for requesting user {RequestingUserId}", userId, requestingUserId);
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var userDto = _mapper.Map<UserResponseDto>(user);
            userDto.Role = roles.FirstOrDefault() ?? "User";

            var userGroup = await _userGroupRepo.Query().FirstOrDefaultAsync(ug => ug.UserId == user.Id);
            if (userGroup != null)
            {
                userDto.GroupId = userGroup.GroupId;
                var group = await _groupRepo.GetByIdAsync(userGroup.GroupId);
                userDto.GroupName = group?.Name;
            }

            return userDto;
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null)
            {
                throw new Exception("Requesting user not found.");
            }

            var requestingUserRoles = await _userManager.GetRolesAsync(requestingUser);
            var isSuperAdmin = requestingUserRoles.Contains("SuperAdmin");

            if (!isSuperAdmin && requestingUser.CompanyId == null)
            {
                throw new Exception("Requesting user has no company association.");
            }

            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                throw new Exception("A user with this email already exists.");
            }

            var validRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            if (!validRoles.Contains(dto.Role))
            {
                throw new Exception($"Invalid role: {dto.Role}");
            }

            var user = new Users
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName?.Trim() ?? string.Empty,
                // If SuperAdmin has a company, assign it to the new user by default (single-company behavior).
                // If they need to create users for other companies, that would require a CompanyId in the DTO.
                CompanyId = requestingUser.CompanyId,
                IsActive = dto.IsActive,
                IsApproved = true,
                IsFirstLogin = true,
                EmailConfirmed = true
            };

            var tempPassword = GenerateTempPassword();
            var result = await _userManager.CreateAsync(user, tempPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("User creation failed: {Errors}", errors);
                throw new Exception($"User creation failed: {errors}");
            }

            await _userManager.AddToRoleAsync(user, dto.Role);

            if (dto.GroupId.HasValue)
            {
                await _userGroupRepo.AddAsync(new UserGroup
                {
                    UserId = user.Id,
                    GroupId = dto.GroupId.Value
                });
                await _userGroupRepo.SaveAsync();
            }

            // AUTO-ASSIGN PERMISSIONS BASED ON ROLE
            try
            {
                await _permissionService.AssignDefaultPermissionsAsync(user.Id, dto.Role);
                _logger.LogInformation("Auto-assigned default permissions to new user {UserId} with role {Role}", user.Id, dto.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-assign permissions for new user {UserId}", user.Id);
            }

            try
            {
                var placeholders = new Dictionary<string, string>
                {
                    { "{{FirstName}}", user.FirstName },
                    { "{{LastName}}", user.LastName },
                    { "{{Email}}", user.Email },
                    { "{{TemporaryPassword}}", tempPassword },
                    { "{{Year}}", DateTime.Now.Year.ToString() }
                };

                await _emailService.SendTemplatedEmailAsync(
                    user.Email,
                    "Welcome to NinjaDAM! Your Account Details",
                    "Registration",
                    placeholders
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
            }

            _logger.LogInformation("User {UserId} created by {RequestingUserId} with role {Role}", user.Id, requestingUserId, dto.Role);

            return await GetUserByIdAsync(user.Id, requestingUserId);
        }

        public async Task<UserResponseDto?> UpdateUserAsync(string userId, UpdateUserDto dto, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null)
            {
                _logger.LogWarning("Requesting user {UserId} not found", requestingUserId);
                return null;
            }

            var requestingUserRoles = await _userManager.GetRolesAsync(requestingUser);
            var isSuperAdmin = requestingUserRoles.Contains("SuperAdmin");

            if (!isSuperAdmin && requestingUser.CompanyId == null)
            {
                _logger.LogWarning("Requesting user {UserId} has no company association", requestingUserId);
                return null;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            if (!isSuperAdmin && user.CompanyId != requestingUser.CompanyId)
            {
                _logger.LogWarning("User {UserId} access denied for requesting user {RequestingUserId}", userId, requestingUserId);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dto.FirstName))
            {
                user.FirstName = dto.FirstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.LastName))
            {
                user.LastName = dto.LastName.Trim();
            }

            if (dto.IsActive.HasValue)
            {
                user.IsActive = dto.IsActive.Value;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                _logger.LogError("User update failed for {UserId}: {Errors}", userId, errors);
                throw new Exception($"User update failed: {errors}");
            }

            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                var validRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                if (!validRoles.Contains(dto.Role))
                {
                    throw new Exception($"Invalid role: {dto.Role}");
                }

                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, dto.Role);

                _logger.LogInformation("User {UserId} role updated to {Role} by {RequestingUserId}", userId, dto.Role, requestingUserId);
            }

            if (dto.GroupId.HasValue)
            {
                var existingUserGroups = await _userGroupRepo.Query()
                    .Where(ug => ug.UserId == user.Id)
                    .ToListAsync();

                foreach (var ug in existingUserGroups)
                {
                    _userGroupRepo.Delete(ug);
                }

                await _userGroupRepo.AddAsync(new UserGroup
                {
                    UserId = user.Id,
                    GroupId = dto.GroupId.Value
                });
                await _userGroupRepo.SaveAsync();
            }

            _logger.LogInformation("User {UserId} updated by {RequestingUserId}", userId, requestingUserId);

            return await GetUserByIdAsync(userId, requestingUserId);
        }

        public async Task<bool> DeleteUserAsync(string userId, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null)
            {
                _logger.LogWarning("Requesting user {UserId} not found", requestingUserId);
                return false;
            }

            var requestingUserRoles = await _userManager.GetRolesAsync(requestingUser);
            var isSuperAdmin = requestingUserRoles.Contains("SuperAdmin");

            if (!isSuperAdmin && requestingUser.CompanyId == null)
            {
                _logger.LogWarning("Requesting user {UserId} has no company association", requestingUserId);
                return false;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return false;
            }

            if (!isSuperAdmin && user.CompanyId != requestingUser.CompanyId)
            {
                _logger.LogWarning("User {UserId} access denied for requesting user {RequestingUserId}", userId, requestingUserId);
                return false;
            }

            if (userId == requestingUserId)
            {
                _logger.LogWarning("User {UserId} attempted to delete their own account", userId);
                throw new Exception("You cannot delete your own account.");
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains("Admin"))
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                // If user has a company, check admins in that company. 
                // If user has no company (e.g. some system user or superadmin scenario), check all admins.
                var companyAdmins = user.CompanyId != null 
                    ? adminUsers.Where(u => u.CompanyId == user.CompanyId).ToList() 
                    : adminUsers.ToList();

                // If the user being deleted is one of the admins, and there's 1 or fewer left, prevent it.
                // Logic: If list count is 1 and that one is THIS user, then deleting them leaves 0.
                if (companyAdmins.Count <= 1 && companyAdmins.Any(a => a.Id == user.Id))
                {
                    _logger.LogWarning("Cannot delete last admin user {UserId} in company", userId);
                    throw new Exception("Cannot delete the last admin user in the organization.");
                }
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("User deletion failed for {UserId}: {Errors}", userId, errors);
                return false;
            }

            _logger.LogInformation("User {UserId} deleted by {RequestingUserId}", userId, requestingUserId);
            return true;
        }

        public async Task<bool> UpdateUserStatusAsync(string userId, UpdateUserStatusDto dto, string requestingUserId)
        {
            var requestingUser = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUser == null)
            {
                _logger.LogWarning("Requesting user {UserId} not found", requestingUserId);
                return false;
            }

            var requestingUserRoles = await _userManager.GetRolesAsync(requestingUser);
            var isSuperAdmin = requestingUserRoles.Contains("SuperAdmin");

            if (!isSuperAdmin && requestingUser.CompanyId == null)
            {
                _logger.LogWarning("Requesting user {UserId} has no company association", requestingUserId);
                return false;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return false;
            }

            if (!isSuperAdmin && user.CompanyId != requestingUser.CompanyId)
            {
                _logger.LogWarning("User {UserId} access denied for requesting user {RequestingUserId}", userId, requestingUserId);
                return false;
            }

            if (userId == requestingUserId && !dto.IsActive)
            {
                _logger.LogWarning("User {UserId} attempted to deactivate their own account", userId);
                throw new Exception("You cannot deactivate your own account.");
            }

            user.IsActive = dto.IsActive;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Status update failed for {UserId}: {Errors}", userId, errors);
                return false;
            }

            _logger.LogInformation("User {UserId} status changed to {Status} by {RequestingUserId}", userId, dto.IsActive ? "Active" : "Inactive", requestingUserId);
            return true;
        }

        public async Task<List<string>> GetAvailableRolesAsync()
        {
            var roles = await _roleManager.Roles
                .Where(r => r.Name != "SuperAdmin")
                .Select(r => r.Name)
                .ToListAsync();

            return roles.OrderBy(r => r).ToList();
        }

        private static string GenerateTempPassword(int length = 12)
        {
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string special = "!@#$%^&*";

            var random = new Random();

            char lowerChar = lower[random.Next(lower.Length)];
            char upperChar = upper[random.Next(upper.Length)];
            char digitChar = digits[random.Next(digits.Length)];
            char specialChar = special[random.Next(special.Length)];

            string allChars = lower + upper + digits + special;
            var remainingChars = new char[length - 4];
            for (int i = 0; i < remainingChars.Length; i++)
                remainingChars[i] = allChars[random.Next(allChars.Length)];

            var passwordChars = new char[length];
            var requiredChars = new char[] { lowerChar, upperChar, digitChar, specialChar };

            requiredChars.CopyTo(passwordChars, 0);
            remainingChars.CopyTo(passwordChars, 4);

            return new string(passwordChars.OrderBy(c => random.Next()).ToArray());
        }
    }
}
