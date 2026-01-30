using Microsoft.AspNetCore.Http;
using NinjaDAM.DTO.Asset;

namespace NinjaDAM.Services.IServices
{
    public interface IAssetService
    {
        Task<PagedAssetResultDto> GetAssetsByFolderAsync(Guid? folderId, string userId, Guid? companyId, string sortBy = "date", string sortDir = "desc", int page = 1, int? pageSize = null);
        Task<AssetDto> GetAssetByIdAsync(Guid assetId, string userId, Guid? companyId);
        Task<IEnumerable<AssetDto>> UploadAssetsAsync(IFormFileCollection files, Guid? folderId, string userId, Guid? companyId);
        Task<DuplicateCheckResultDto> CheckForDuplicatesAsync(IFormFileCollection files, string userId, Guid? companyId);
        Task<DuplicateCheckResultDto> CheckForDuplicatesByChecksumAsync(CheckDuplicatesRequestDto request, string userId, Guid? companyId);
        Task<UploadWithDuplicateResultDto> UploadAssetsWithDuplicateHandlingAsync(IFormFileCollection files, List<DuplicateActionDto> actions, Guid? folderId, string userId, Guid? companyId);
        Task<AssetDto> UpdateAssetAsync(Guid assetId, string userId, Guid? companyId, UpdateAssetDto dto);
        Task<int> UpdateAssetsMetadataAsync(List<Guid> assetIds, string userId, Guid? companyId, string key, string value);
        Task<bool> MoveAssetAsync(Guid assetId, Guid newFolderId, string userId, Guid? companyId);
        Task<bool> DeleteAssetAsync(Guid assetId, string userId, Guid? companyId);
        Task<bool> DeleteAssetsAsync(List<Guid> assetIds, string userId, Guid? companyId);
        Task<byte[]> DownloadAssetAsync(Guid assetId, string userId, Guid? companyId);
        Task<byte[]> DownloadAssetsAsZipAsync(List<Guid> assetIds, string userId, Guid? companyId);
        Task<PagedAssetResultDto> SearchAssetsAsync(string userId, Guid? companyId, Guid? folderId = null, string? fileType = null, string? keyword = null, string sortBy = "date", string sortDir = "desc", int page = 1, int? pageSize = null, List<Guid>? assetIds = null);
        Task<PagedAssetResultDto> AdvancedSearchAssetsAsync(string userId, Guid? companyId, AdvancedSearchRequestDto request);
        
        // Recycle Bin
        Task<PagedAssetResultDto> GetDeletedAssetsAsync(string userId, Guid? companyId, string sortBy = "date", string sortDir = "desc", int page = 1, int? pageSize = null);
        Task<bool> RestoreAssetAsync(Guid assetId, string userId, Guid? companyId);
        Task<bool> RestoreAssetsAsync(List<Guid> assetIds, string userId, Guid? companyId);
        Task<bool> PermanentlyDeleteAssetAsync(Guid assetId, string userId, Guid? companyId);
        Task<bool> PermanentlyDeleteAssetsAsync(List<Guid> assetIds, string userId, Guid? companyId);
        Task<int> PermanentlyDeleteExpiredAssetsAsync();
        Task<int> ExtractIptcForExistingAssetsAsync(string userId, Guid? companyId);
        Task<int> GetTotalAssetCountAsync(string userId, Guid? companyId);
    }
}
