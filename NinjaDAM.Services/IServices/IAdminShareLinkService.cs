using NinjaDAM.DTO.Admin;

namespace NinjaDAM.Services.IServices
{
    public interface IAdminShareLinkService
    {
        Task<IEnumerable<AdminShareLinkDto>> GetAllActiveShareLinksAsync();
        Task<bool> DeleteShareLinkAsync(Guid shareLinkId, string shareLinkType, string adminUserId, string adminUserName, string? ipAddress);
        Task<bool> UpdateShareLinkAsync(UpdateShareLinkDto updateDto, string adminUserId, string adminUserName, string? ipAddress);
        Task<IEnumerable<ShareLinkAuditLogDto>> GetShareLinkAuditLogsAsync(Guid? shareLinkId = null, int limit = 100);
    }
}
