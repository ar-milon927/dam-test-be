using NinjaDAM.DTO.CollectionShare;

namespace NinjaDAM.Services.IServices
{
    public interface ICollectionShareService
    {
        Task<CollectionShareLinkDto> CreateShareLinkAsync(CreateShareLinkDto createDto, string userId);
        Task<SharedCollectionDto?> GetSharedCollectionAsync(string token);
        Task<bool> RevokeShareLinkAsync(Guid shareLinkId, string userId);
        Task<IEnumerable<CollectionShareLinkDto>> GetActiveShareLinksAsync(Guid collectionId, string userId);
        Task IncrementDownloadCountAsync(string token);
        Task<int> CleanupExpiredLinksAsync();
        Task<(Stream FileStream, string ContentType, string FileName)?> DownloadAssetFromSharedCollectionAsync(string token, Guid assetId);
    }
}
