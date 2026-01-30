using NinjaDAM.DTO.UserManagement;

namespace NinjaDAM.Services.IServices
{
    public interface IUserManagementService
    {
        Task<List<UserResponseDto>> GetAllUsersAsync(string requestingUserId);
        Task<UserResponseDto?> GetUserByIdAsync(string userId, string requestingUserId);
        Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, string requestingUserId);
        Task<UserResponseDto?> UpdateUserAsync(string userId, UpdateUserDto dto, string requestingUserId);
        Task<bool> DeleteUserAsync(string userId, string requestingUserId);
        Task<bool> UpdateUserStatusAsync(string userId, UpdateUserStatusDto dto, string requestingUserId);
        Task<List<string>> GetAvailableRolesAsync();
    }
}
