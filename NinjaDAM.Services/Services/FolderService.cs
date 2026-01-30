using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.Folder;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class FolderService : IFolderService
    {
        private readonly IRepository<Folder> _folderRepo;
        private readonly IRepository<Asset> _assetRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<FolderService> _logger;

        public FolderService(
            IRepository<Folder> folderRepo,
            IRepository<Asset> assetRepo,
            IMapper mapper,
            ILogger<FolderService> logger)
        {
            _folderRepo = folderRepo;
            _assetRepo = assetRepo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<FolderDto>> GetUserFoldersAsync(string userId)
        {
            var folders = await _folderRepo
                .Query()
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.Level)
                .ThenBy(f => f.Name)
                .ToListAsync();

            if (folders.Any())
            {
                var directCounts = await _assetRepo
                    .Query()
                    .Where(a => a.UserId == userId && !a.IsDeleted && (a.CompanyId == null || a.CompanyId != null))
                    .GroupBy(a => a.FolderId)
                    .Where(g => g.Key.HasValue)
                    .Select(g => new { FolderId = g.Key.Value, Count = g.Count() })
                    .ToDictionaryAsync(k => k.FolderId, v => v.Count);

                foreach (var folder in folders)
                {
                    folder.AssetCount = directCounts.ContainsKey(folder.Id) ? directCounts[folder.Id] : 0;
                }

                var folderMap = folders.ToDictionary(f => f.Id);
                var foldersByLevelDesc = folders.OrderByDescending(f => f.Level).ToList();

                foreach (var folder in foldersByLevelDesc)
                {
                    if (folder.ParentId.HasValue && folderMap.TryGetValue(folder.ParentId.Value, out var parent))
                    {
                        parent.AssetCount += folder.AssetCount;
                    }
                }
            }

            return _mapper.Map<IEnumerable<FolderDto>>(folders);
        }

        public async Task<FolderDto> GetFolderByIdAsync(Guid folderId, string userId)
        {
            var folder = await _folderRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId);

            if (folder == null)
            {
                _logger.LogWarning("Folder {FolderId} not found for user {UserId}", folderId, userId);
                return null;
            }

            if (folder != null)
            {
                folder.AssetCount = await _assetRepo
                    .Query()
                    .CountAsync(a => a.FolderId == folder.Id && !a.IsDeleted);
            }

            return _mapper.Map<FolderDto>(folder);
        }

        private async Task GetDescendantIdsRecursive(Guid parentId, List<Guid> result)
        {
            var childrenIds = await _folderRepo
                .Query()
                .Where(f => f.ParentId == parentId)
                .Select(f => f.Id)
                .ToListAsync();

            if (childrenIds.Any())
            {
                result.AddRange(childrenIds);
                foreach (var childId in childrenIds)
                {
                    await GetDescendantIdsRecursive(childId, result);
                }
            }
        }

        public async Task<FolderDto> CreateFolderAsync(CreateFolderDto dto, string userId, Guid? companyId)
        {
            // Calculate level if not provided
            int level = dto.Level ?? 0;
            if (dto.ParentId.HasValue)
            {
                var parentFolder = await _folderRepo
                    .Query()
                    .FirstOrDefaultAsync(f => f.Id == dto.ParentId.Value && f.UserId == userId);
                    
                if (parentFolder != null)
                {
                    level = parentFolder.Level + 1;
                }
                else
                {
                    throw new Exception("Parent folder not found or access denied");
                }
            }

            var folder = new Folder
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                ParentId = dto.ParentId,
                UserId = userId,
                CompanyId = companyId,
                Level = level,
                AssetCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _folderRepo.AddAsync(folder);
            await _folderRepo.SaveAsync();

            _logger.LogInformation("Folder {FolderId} created by user {UserId}", folder.Id, userId);

            return _mapper.Map<FolderDto>(folder);
        }

        public async Task<FolderDto> UpdateFolderAsync(Guid folderId, CreateFolderDto dto, string userId)
        {
            var folder = await _folderRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId);

            if (folder == null)
            {
                throw new Exception("Folder not found or access denied");
            }

            folder.Name = dto.Name;
            folder.UpdatedAt = DateTime.UtcNow;

            _folderRepo.Update(folder);
            await _folderRepo.SaveAsync();

            _logger.LogInformation("Folder {FolderId} updated by user {UserId}", folder.Id, userId);

            return _mapper.Map<FolderDto>(folder);
        }

        public async Task<bool> DeleteFolderAsync(Guid folderId, string userId)
        {
            var folder = await _folderRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId);

            if (folder == null)
            {
                _logger.LogWarning("Folder {FolderId} not found for deletion by user {UserId}", folderId, userId);
                return false;
            }

            // Delete all assets in this folder
            var assets = await _assetRepo
                .Query()
                .Where(a => a.FolderId == folderId)
                .ToListAsync();

            foreach (var asset in assets)
            {
                // Delete physical file
                if (File.Exists(asset.FilePath))
                {
                    File.Delete(asset.FilePath);
                }
                if (!string.IsNullOrEmpty(asset.ThumbnailPath) && File.Exists(asset.ThumbnailPath))
                {
                    File.Delete(asset.ThumbnailPath);
                }
                _assetRepo.Delete(asset);
            }

            // Delete all subfolders recursively
            var subFolders = await _folderRepo
                .Query()
                .Where(f => f.ParentId == folderId)
                .ToListAsync();

            foreach (var subFolder in subFolders)
            {
                await DeleteFolderAsync(subFolder.Id, userId);
            }

            _folderRepo.Delete(folder);
            await _folderRepo.SaveAsync();

            _logger.LogInformation("Folder {FolderId} deleted by user {UserId}", folderId, userId);

            return true;
        }

        public async Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
        {
            var folder = await _folderRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId);

            if (folder == null)
            {
                _logger.LogWarning("Folder {FolderId} not found for move by user {UserId}", folderId, userId);
                return false;
            }

            // Prevent moving folder into itself or its descendants
            if (newParentId.HasValue && await IsDescendantOf(folderId, newParentId.Value))
            {
                throw new Exception("Cannot move folder into itself or its descendants");
            }

            // Calculate new level
            int newLevel = 0;
            if (newParentId.HasValue)
            {
                var newParent = await _folderRepo
                    .Query()
                    .FirstOrDefaultAsync(f => f.Id == newParentId.Value && f.UserId == userId);
                    
                if (newParent != null)
                {
                    newLevel = newParent.Level + 1;
                }
                else
                {
                    throw new Exception("Target parent folder not found or access denied");
                }
            }

            int oldLevel = folder.Level;
            folder.ParentId = newParentId;
            folder.Level = newLevel;
            folder.UpdatedAt = DateTime.UtcNow;

            _folderRepo.Update(folder);
            await _folderRepo.SaveAsync();

            // Update all descendant folders' levels
            int levelDiff = newLevel - oldLevel;
            await UpdateDescendantLevels(folderId, levelDiff);

            _logger.LogInformation("Folder {FolderId} moved by user {UserId}", folderId, userId);

            return true;
        }

        private async Task<bool> IsDescendantOf(Guid folderId, Guid potentialAncestorId)
        {
            var folder = await _folderRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == potentialAncestorId);

            while (folder != null && folder.ParentId.HasValue)
            {
                if (folder.ParentId.Value == folderId)
                {
                    return true;
                }
                folder = await _folderRepo
                    .Query()
                    .FirstOrDefaultAsync(f => f.Id == folder.ParentId.Value);
            }

            return false;
        }

        private async Task UpdateDescendantLevels(Guid parentId, int levelDiff)
        {
            var children = await _folderRepo
                .Query()
                .Where(f => f.ParentId == parentId)
                .ToListAsync();

            foreach (var child in children)
            {
                child.Level += levelDiff;
                child.UpdatedAt = DateTime.UtcNow;
                _folderRepo.Update(child);

                // Recursively update descendants
                await UpdateDescendantLevels(child.Id, levelDiff);
            }

            await _folderRepo.SaveAsync();
        }
    }
}
