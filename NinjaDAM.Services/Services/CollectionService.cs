using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.AssetCollection;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly IRepository<Collection> _collectionRepo;
        private readonly IRepository<CollectionAsset> _collectionAssetRepo;
        private readonly IRepository<Asset> _assetRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<CollectionService> _logger;

        public CollectionService(
            IRepository<Collection> collectionRepo,
            IRepository<CollectionAsset> collectionAssetRepo,
            IRepository<Asset> assetRepo,
            IMapper mapper,
            ILogger<CollectionService> logger)
        {
            _collectionRepo = collectionRepo;
            _collectionAssetRepo = collectionAssetRepo;
            _assetRepo = assetRepo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<CollectionDto>> GetUserCollectionsAsync(string userId)
        {
            var collections = await _collectionRepo
                .Query()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<CollectionDto>>(collections);
        }

        public async Task<CollectionWithAssetsDto> GetCollectionByIdAsync(Guid collectionId, string userId)
        {
            var collection = await _collectionRepo
                .Query()
                .Include(c => c.CollectionAssets)
                .ThenInclude(ca => ca.Asset)
                .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

            if (collection == null)
            {
                _logger.LogWarning("Collection {CollectionId} not found for user {UserId}", collectionId, userId);
                return null;
            }

            var collectionDto = _mapper.Map<CollectionWithAssetsDto>(collection);
            
            // Map assets from CollectionAssets
            var assets = collection.CollectionAssets
                .Select(ca => ca.Asset)
                .Where(a => a != null)
                .ToList();

            collectionDto.Assets = _mapper.Map<IEnumerable<NinjaDAM.DTO.Asset.AssetDto>>(assets);

            return collectionDto;
        }

        public async Task<CollectionDto> CreateCollectionAsync(CreateCollectionDto dto, string userId, Guid? companyId)
        {
            // Check for duplicate name
            var existingCollection = await _collectionRepo
                .Query()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Name.Trim().ToLower() == dto.Name.Trim().ToLower());

            if (existingCollection != null)
            {
                throw new Exception("A collection with this name already exists");
            }

            var collection = _mapper.Map<Collection>(dto);
            collection.Id = Guid.NewGuid();
            collection.UserId = userId;
            collection.CompanyId = companyId;
            collection.AssetCount = 0;
            collection.CreatedAt = DateTime.UtcNow;
            collection.UpdatedAt = DateTime.UtcNow;

            await _collectionRepo.AddAsync(collection);
            await _collectionRepo.SaveAsync();

            _logger.LogInformation("Collection {CollectionId} created by user {UserId}", collection.Id, userId);

            return _mapper.Map<CollectionDto>(collection);
        }

        public async Task<CollectionDto> UpdateCollectionAsync(Guid collectionId, UpdateCollectionDto dto, string userId)
        {
            var collection = await _collectionRepo
                .Query()
                .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

            if (collection == null)
            {
                _logger.LogWarning("Collection {CollectionId} not found for update by user {UserId}", collectionId, userId);
                return null;
            }

            // Check for duplicate name (excluding current collection)
            var existingCollection = await _collectionRepo
                .Query()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Id != collectionId && c.Name.Trim().ToLower() == dto.Name.Trim().ToLower());

            if (existingCollection != null)
            {
                throw new Exception("A collection with this name already exists");
            }

            collection.Name = dto.Name;
            collection.Description = dto.Description;
            
            if (dto.CoverPhotoAssetId.HasValue)
            {
                var asset = await _assetRepo
                    .Query()
                    .FirstOrDefaultAsync(a => a.Id == dto.CoverPhotoAssetId.Value && a.UserId == userId);
                
                if (asset == null)
                {
                    throw new Exception("Cover photo asset not found or doesn't belong to you");
                }
                
                if (asset.FileType?.ToLower() != "image")
                {
                    throw new Exception("Cover photo must be an image asset");
                }
                
                collection.CoverPhotoAssetId = dto.CoverPhotoAssetId.Value;
            }
            
            collection.UpdatedAt = DateTime.UtcNow;

            _collectionRepo.Update(collection);
            await _collectionRepo.SaveAsync();

            _logger.LogInformation("Collection {CollectionId} updated by user {UserId}", collectionId, userId);

            return _mapper.Map<CollectionDto>(collection);
        }

        public async Task<bool> DeleteCollectionAsync(Guid collectionId, string userId)
        {
            var collection = await _collectionRepo
                .Query()
                .Include(c => c.CollectionAssets)
                .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

            if (collection == null)
            {
                _logger.LogWarning("Collection {CollectionId} not found for deletion by user {UserId}", collectionId, userId);
                return false;
            }

            // Delete all collection-asset relationships first
            foreach (var collectionAsset in collection.CollectionAssets)
            {
                _collectionAssetRepo.Delete(collectionAsset);
            }

            _collectionRepo.Delete(collection);
            await _collectionRepo.SaveAsync();

            _logger.LogInformation("Collection {CollectionId} deleted by user {UserId}", collectionId, userId);

            return true;
        }

        public async Task<int> AddAssetsToCollectionAsync(Guid collectionId, List<Guid> assetIds, string userId)
        {
            var collection = await _collectionRepo
                .Query()
                .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

            if (collection == null)
            {
                _logger.LogWarning("Collection {CollectionId} not found for user {UserId}", collectionId, userId);
                return -1; // Return -1 to indicate collection not found
            }

            // Verify all assets belong to the user
            var assets = await _assetRepo
                .Query()
                .Where(a => assetIds.Contains(a.Id) && a.UserId == userId)
                .ToListAsync();

            if (assets.Count != assetIds.Count)
            {
                _logger.LogWarning("Some assets not found or don't belong to user {UserId}", userId);
                return -1; // Return -1 to indicate assets not found/invalid
            }

            // Get existing asset IDs in this collection
            var existingAssetIds = await _collectionAssetRepo
                .Query()
                .Where(ca => ca.CollectionId == collectionId)
                .Select(ca => ca.AssetId)
                .ToListAsync();

            // Add only new assets (avoid duplicates)
            var newAssetIds = assetIds.Except(existingAssetIds).ToList();

            foreach (var assetId in newAssetIds)
            {
                var collectionAsset = new CollectionAsset
                {
                    Id = Guid.NewGuid(),
                    CollectionId = collectionId,
                    AssetId = assetId,
                    AddedAt = DateTime.UtcNow
                };

                await _collectionAssetRepo.AddAsync(collectionAsset);
            }

            // Update asset count
            collection.AssetCount = await _collectionAssetRepo
                .Query()
                .CountAsync(ca => ca.CollectionId == collectionId);
            collection.UpdatedAt = DateTime.UtcNow;

            _collectionRepo.Update(collection);
            await _collectionRepo.SaveAsync();

            _logger.LogInformation("{Count} assets added to collection {CollectionId} by user {UserId}", 
                newAssetIds.Count, collectionId, userId);

            return newAssetIds.Count; // Return count of newly added assets
        }

        public async Task<bool> RemoveAssetFromCollectionAsync(Guid collectionId, Guid assetId, string userId)
        {
            var collection = await _collectionRepo
                .Query()
                .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

            if (collection == null)
            {
                _logger.LogWarning("Collection {CollectionId} not found for user {UserId}", collectionId, userId);
                return false;
            }

            var collectionAsset = await _collectionAssetRepo
                .Query()
                .FirstOrDefaultAsync(ca => ca.CollectionId == collectionId && ca.AssetId == assetId);

            if (collectionAsset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found in collection {CollectionId}", assetId, collectionId);
                return false;
            }

            _collectionAssetRepo.Delete(collectionAsset);
            await _collectionAssetRepo.SaveAsync();

            // Update asset count after deletion
            collection.AssetCount = await _collectionAssetRepo
                .Query()
                .CountAsync(ca => ca.CollectionId == collectionId);
            collection.UpdatedAt = DateTime.UtcNow;

            _collectionRepo.Update(collection);
            await _collectionRepo.SaveAsync();

            _logger.LogInformation("Asset {AssetId} removed from collection {CollectionId} by user {UserId}", 
                assetId, collectionId, userId);

            return true;
        }
    }
}
