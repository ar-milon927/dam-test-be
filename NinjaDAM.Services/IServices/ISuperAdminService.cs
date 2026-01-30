using NinjaDAM.DTO.SuperAdmin;

namespace NinjaDAM.Services.IServices
{
    public interface ISuperAdminService
    {
        Task<IEnumerable<PendingUserDto>> GetPendingUsersAsync();
        Task<string> ApproveUserAsync(Guid userId);
        Task<string> RejectUserAsync(Guid userId);
    }
}
