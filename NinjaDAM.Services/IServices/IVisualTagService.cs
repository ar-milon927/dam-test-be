using NinjaDAM.DTO.VisualTag;

namespace NinjaDAM.Services.IServices
{
    public interface IVisualTagService
    {
        // CRUD operations
        Task<IEnumerable<VisualTagDto>> GetAllAsync(string userId);
        Task<VisualTagDto> GetByIdAsync(Guid tagId, string userId);
        Task<VisualTagDto> CreateAsync(CreateVisualTagDto dto, string userId, Guid? companyId);
        Task<VisualTagDto> UpdateAsync(Guid tagId, UpdateVisualTagDto dto, string userId);
        Task<bool> DeleteAsync(Guid tagId, string userId);

        // Tag assignment operations
        Task<int> AssignTagsToAssetsAsync(AssignTagsDto dto, string userId);
        Task<int> RemoveTagsFromAssetsAsync(RemoveTagsDto dto, string userId);
        
        // Get tags for specific asset
        Task<IEnumerable<VisualTagDto>> GetTagsByAssetIdAsync(Guid assetId, string userId);
        
        // Get assets by tag
        Task<IEnumerable<Guid>> GetAssetIdsByTagIdAsync(Guid tagId, string userId);
    }
}
