using NinjaDAM.DTO.AssetShare;

namespace NinjaDAM.Services.IServices
{
    public interface IAssetShareService
    {
        Task<AssetShareLinkDto> CreateShareLinkAsync(CreateAssetShareLinkDto createDto, string userId);
        Task<SharedAssetDetailDto?> GetSharedAssetAsync(string token);
        Task<bool> RevokeShareLinkAsync(Guid shareLinkId, string userId);
        Task<IEnumerable<AssetShareLinkDto>> GetActiveShareLinksAsync(Guid assetId, string userId);
        Task IncrementDownloadCountAsync(string token);
        Task<int> CleanupExpiredLinksAsync();
    }
}
