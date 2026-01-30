using NinjaDAM.DTO.AssetCollection;

namespace NinjaDAM.Services.IServices
{
    public interface ICollectionService
    {
        Task<IEnumerable<CollectionDto>> GetUserCollectionsAsync(string userId);
        Task<CollectionWithAssetsDto> GetCollectionByIdAsync(Guid collectionId, string userId);
        Task<CollectionDto> CreateCollectionAsync(CreateCollectionDto dto, string userId, Guid? companyId);
        Task<CollectionDto> UpdateCollectionAsync(Guid collectionId, UpdateCollectionDto dto, string userId);
        Task<bool> DeleteCollectionAsync(Guid collectionId, string userId);
        Task<int> AddAssetsToCollectionAsync(Guid collectionId, List<Guid> assetIds, string userId);
        Task<bool> RemoveAssetFromCollectionAsync(Guid collectionId, Guid assetId, string userId);
    }
}
