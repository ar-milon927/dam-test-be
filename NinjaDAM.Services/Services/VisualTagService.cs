using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.Services.IServices;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DTO.VisualTag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{
    public class VisualTagService : IVisualTagService
    {
        private readonly IRepository<VisualTag> _tagRepo;
        private readonly IRepository<AssetTag> _assetTagRepo;
        private readonly IRepository<Asset> _assetRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<VisualTagService> _logger;

        public VisualTagService(
            IRepository<VisualTag> tagRepo,
            IRepository<AssetTag> assetTagRepo,
            IRepository<Asset> assetRepo,
            IMapper mapper,
            ILogger<VisualTagService> logger)
        {
            _tagRepo = tagRepo;
            _assetTagRepo = assetTagRepo;
            _assetRepo = assetRepo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<VisualTagDto>> GetAllAsync(string userId)
        {
            var tags = await _tagRepo
                .Query()
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.Name)
                .ToListAsync();

            return _mapper.Map<IEnumerable<VisualTagDto>>(tags);
        }

        public async Task<VisualTagDto> GetByIdAsync(Guid tagId, string userId)
        {
            var tag = await _tagRepo
                .Query()
                .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

            if (tag == null)
            {
                _logger.LogWarning("Visual tag {TagId} not found for user {UserId}", tagId, userId);
                return null;
            }

            return _mapper.Map<VisualTagDto>(tag);
        }

        public async Task<VisualTagDto> CreateAsync(CreateVisualTagDto dto, string userId, Guid? companyId)
        {
            // Check for duplicate tag name
            var existingTag = await _tagRepo
                .Query()
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Name.Trim().ToLower() == dto.Name.Trim().ToLower());

            if (existingTag != null)
            {
                throw new Exception($"A tag with the name '{dto.Name}' already exists.");
            }

            var tag = _mapper.Map<VisualTag>(dto);
            tag.Id = Guid.NewGuid();
            tag.Name = dto.Name.Trim();
            tag.UserId = userId;
            tag.CompanyId = companyId;
            tag.CreatedAt = DateTime.UtcNow;
            tag.UpdatedAt = DateTime.UtcNow;

            await _tagRepo.AddAsync(tag);
            await _tagRepo.SaveAsync();

            _logger.LogInformation("Visual tag {TagId} created by user {UserId}", tag.Id, userId);

            return _mapper.Map<VisualTagDto>(tag);
        }

        public async Task<VisualTagDto> UpdateAsync(Guid tagId, UpdateVisualTagDto dto, string userId)
        {
            var tag = await _tagRepo
                .Query()
                .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

            if (tag == null)
            {
                _logger.LogWarning("Visual tag {TagId} not found for update by user {UserId}", tagId, userId);
                return null;
            }

            // Check for duplicate name (excluding current tag)
            var existingTag = await _tagRepo
                .Query()
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Name.Trim().ToLower() == dto.Name.Trim().ToLower() && t.Id != tagId);

            if (existingTag != null)
            {
                throw new Exception($"A tag with the name '{dto.Name}' already exists.");
            }

            tag.Name = dto.Name.Trim();
            tag.Color = dto.Color;
            tag.UpdatedAt = DateTime.UtcNow;

            _tagRepo.Update(tag);
            await _tagRepo.SaveAsync();

            _logger.LogInformation("Visual tag {TagId} updated by user {UserId}", tagId, userId);

            return _mapper.Map<VisualTagDto>(tag);
        }

        public async Task<bool> DeleteAsync(Guid tagId, string userId)
        {
            var tag = await _tagRepo
                .Query()
                .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

            if (tag == null)
            {
                _logger.LogWarning("Visual tag {TagId} not found for deletion by user {UserId}", tagId, userId);
                return false;
            }

            // Delete all asset-tag associations
            var assetTags = await _assetTagRepo
                .Query()
                .Where(at => at.VisualTagId == tagId)
                .ToListAsync();

            foreach (var assetTag in assetTags)
            {
                _assetTagRepo.Delete(assetTag);
            }

            _tagRepo.Delete(tag);
            await _tagRepo.SaveAsync();

            _logger.LogInformation("Visual tag {TagId} deleted by user {UserId}", tagId, userId);

            return true;
        }

        public async Task<int> AssignTagsToAssetsAsync(AssignTagsDto dto, string userId)
        {
            // Verify all assets belong to user
            var assets = await _assetRepo
                .Query()
                .Where(a => dto.AssetIds.Contains(a.Id) && a.UserId == userId)
                .ToListAsync();

            if (assets.Count != dto.AssetIds.Count)
            {
                _logger.LogWarning("Some assets not found or access denied for user {UserId}", userId);
                throw new Exception("Some assets not found or you don't have access to them.");
            }

            // Verify all tags belong to user
            var tags = await _tagRepo
                .Query()
                .Where(t => dto.TagIds.Contains(t.Id) && t.UserId == userId)
                .ToListAsync();

            if (tags.Count != dto.TagIds.Count)
            {
                _logger.LogWarning("Some tags not found or access denied for user {UserId}", userId);
                throw new Exception("Some tags not found or you don't have access to them.");
            }

            // Get existing asset-tag combinations to avoid duplicates
            var existingAssetTags = await _assetTagRepo
                .Query()
                .Where(at => dto.AssetIds.Contains(at.AssetId) && dto.TagIds.Contains(at.VisualTagId))
                .Select(at => new { at.AssetId, at.VisualTagId })
                .ToListAsync();

            var existingSet = existingAssetTags.Select(at => $"{at.AssetId}_{at.VisualTagId}").ToHashSet();

            // Create new asset-tag associations
            var newAssignments = 0;
            foreach (var assetId in dto.AssetIds)
            {
                foreach (var tagId in dto.TagIds)
                {
                    var key = $"{assetId}_{tagId}";
                    if (!existingSet.Contains(key))
                    {
                        var assetTag = new AssetTag
                        {
                            Id = Guid.NewGuid(),
                            AssetId = assetId,
                            VisualTagId = tagId,
                            AssignedAt = DateTime.UtcNow
                        };

                        await _assetTagRepo.AddAsync(assetTag);
                        newAssignments++;
                    }
                }
            }

            if (newAssignments > 0)
            {
                await _assetTagRepo.SaveAsync();

                // Update asset counts for affected tags
                foreach (var tagId in dto.TagIds)
                {
                    var tag = tags.First(t => t.Id == tagId);
                    tag.AssetCount = await _assetTagRepo.Query().CountAsync(at => at.VisualTagId == tagId);
                    _tagRepo.Update(tag);
                }

                await _tagRepo.SaveAsync();

                _logger.LogInformation("{Count} tag assignments created for user {UserId}", newAssignments, userId);
            }

            return newAssignments;
        }

        public async Task<int> RemoveTagsFromAssetsAsync(RemoveTagsDto dto, string userId)
        {
            // Get asset-tag associations that belong to user's assets
            var assetTags = await _assetTagRepo
                .Query()
                .Include(at => at.Asset)
                .Where(at => dto.AssetIds.Contains(at.AssetId) &&
                             dto.TagIds.Contains(at.VisualTagId) &&
                             at.Asset.UserId == userId)
                .ToListAsync();

            if (!assetTags.Any())
            {
                _logger.LogWarning("No matching tag assignments found for user {UserId}", userId);
                return 0;
            }

            var affectedTagIds = assetTags.Select(at => at.VisualTagId).Distinct().ToList();

            foreach (var assetTag in assetTags)
            {
                _assetTagRepo.Delete(assetTag);
            }

            await _assetTagRepo.SaveAsync();

            // Update asset counts for affected tags
            foreach (var tagId in affectedTagIds)
            {
                var tag = await _tagRepo
                    .Query()
                    .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

                if (tag != null)
                {
                    tag.AssetCount = await _assetTagRepo.Query().CountAsync(at => at.VisualTagId == tagId);
                    _tagRepo.Update(tag);
                }
            }

            await _tagRepo.SaveAsync();

            _logger.LogInformation("{Count} tag assignments removed for user {UserId}", assetTags.Count, userId);

            return assetTags.Count;
        }

        public async Task<IEnumerable<VisualTagDto>> GetTagsByAssetIdAsync(Guid assetId, string userId)
        {
            // Verify asset belongs to user
            var asset = await _assetRepo
                .Query()
                .FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == userId);

            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for user {UserId}", assetId, userId);
                return Enumerable.Empty<VisualTagDto>();
            }

            var tags = await _assetTagRepo
                .Query()
                .Where(at => at.AssetId == assetId)
                .Include(at => at.VisualTag)
                .Select(at => at.VisualTag)
                .OrderBy(t => t.Name)
                .ToListAsync();

            return _mapper.Map<IEnumerable<VisualTagDto>>(tags);
        }

        public async Task<IEnumerable<Guid>> GetAssetIdsByTagIdAsync(Guid tagId, string userId)
        {
            // Verify tag belongs to user
            var tag = await _tagRepo
                .Query()
                .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

            if (tag == null)
            {
                _logger.LogWarning("Visual tag {TagId} not found for user {UserId}", tagId, userId);
                return Enumerable.Empty<Guid>();
            }

            var assetIds = await _assetTagRepo
                .Query()
                .Where(at => at.VisualTagId == tagId)
                .Select(at => at.AssetId)
                .ToListAsync();

            return assetIds;
        }
    }
}
