using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.Asset;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.Extensions;
using NinjaDAM.Services.IServices;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;

namespace NinjaDAM.Services.Services
{
    public class AssetService : IAssetService
    {
        private readonly IRepository<Asset> _assetRepo;
        private readonly IRepository<Folder> _folderRepo;
        private readonly IRepository<Users> _userRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<AssetService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IThumbnailService _thumbnailService;
        private readonly IIptcExtractionService _iptcExtractionService;
        private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(DbFunctionsExtensions.Like) && m.GetParameters().Length == 4 && m.GetParameters()[1].ParameterType == typeof(string));
        private static readonly MethodInfo GuidListContainsMethod = typeof(List<Guid>).GetMethod(nameof(List<Guid>.Contains), new[] { typeof(Guid) });
        private static readonly MethodInfo StringListContainsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) });
        private static readonly MethodInfo AnyAssetTagMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(AssetTag));

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
            ".tiff", ".tif", ".heic", ".heif", ".psd",
            ".arw", ".cr2", ".nef", ".dng", ".raw"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm",
            ".flv", ".wmv", ".m4v", ".mpeg", ".mpg"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg",
            ".m4a", ".wma", ".aiff"
        };

        public AssetService(
            IRepository<Asset> assetRepo,
            IRepository<Folder> folderRepo,
            IRepository<Users> userRepo,
            IMapper mapper,
            ILogger<AssetService> logger,
            IWebHostEnvironment environment,
            IThumbnailService thumbnailService,
            IIptcExtractionService iptcExtractionService)
        {
            _assetRepo = assetRepo;
            _folderRepo = folderRepo;
            _userRepo = userRepo;
            _mapper = mapper;
            _logger = logger;
            _environment = environment;
            _thumbnailService = thumbnailService;
            _iptcExtractionService = iptcExtractionService;
        }

        private async Task<List<Guid>> GetAllDescendantFolderIdsAsync(Guid parentFolderId)
        {
            var allFolderIds = new List<Guid> { parentFolderId };
            var childFolders = await _folderRepo.Query().Where(f => f.ParentId == parentFolderId).ToListAsync();
            
            foreach (var child in childFolders)
            {
                var descendantIds = await GetAllDescendantFolderIdsAsync(child.Id);
                allFolderIds.AddRange(descendantIds);
            }
            
            return allFolderIds;
        }

        public async Task<PagedAssetResultDto> GetAssetsByFolderAsync(Guid? folderId, string userId, Guid? companyId, string sortBy = "date", string sortDir = "desc", int page = 1, int? pageSize = null)
        {
            var query = _assetRepo.Query();

            if (companyId.HasValue)
            {
                query = query.Where(a => a.CompanyId == companyId.Value && !a.IsDeleted);
            }
            else
            {
                // Root organization (null company) shared visibility
                query = query.Where(a => a.CompanyId == null && !a.IsDeleted);
            }

            if (folderId.HasValue)
            {
                var folderIds = await GetAllDescendantFolderIdsAsync(folderId.Value);
                query = query.Where(a => folderIds.Contains(a.FolderId.Value));
            }
            else
            {
            }

            // total before paging
            var total = await query.CountAsync();

            // Sorting
            // Sorting
            query = ApplySorting(query, sortBy, sortDir);

            // Paging
            if (pageSize.HasValue && pageSize.Value > 0)
            {
                var skip = (page - 1) * pageSize.Value;
                query = query.Skip(skip).Take(pageSize.Value);
            }

            var assets = await query.ToListAsync();

            return new PagedAssetResultDto
            {
                Assets = _mapper.Map<IEnumerable<AssetDto>>(assets),
                Total = total,
                Page = page,
                HasMore = pageSize.HasValue && (page * pageSize.Value) < total
            };
        }

        public async Task<AssetDto> GetAssetByIdAsync(Guid assetId, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId);

            if (companyId.HasValue)
            {
                query = query.Where(a => a.CompanyId == companyId.Value);
            }
            else
            {
                query = query.Where(a => a.CompanyId == null);
            }

            var asset = await query.FirstOrDefaultAsync();

            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for user {UserId} (Company: {CompanyId})", assetId, userId, companyId);
                return null;
            }

            return _mapper.Map<AssetDto>(asset);
        }

        public async Task<IEnumerable<AssetDto>> UploadAssetsAsync(IFormFileCollection files, Guid? folderId, string userId, Guid? companyId)
        {
            if (files == null || files.Count == 0)
            {
                throw new Exception("No files provided");
            }

            // Verify folder ownership if folderId is provided
            if (folderId.HasValue)
            {
                var folderQuery = _folderRepo.Query().Where(f => f.Id == folderId.Value);
                
                if (companyId.HasValue)
                    folderQuery = folderQuery.Where(f => f.CompanyId == companyId.Value);
                else
                    folderQuery = folderQuery.Where(f => f.CompanyId == null);

                var folder = await folderQuery.FirstOrDefaultAsync();

                if (folder == null)
                {
                    throw new Exception("Folder not found or access denied");
                }
            }

            // Get existing filenames in this folder for duplicate checking
            var existingQuery = _assetRepo.Query().Where(a => a.FolderId == folderId);
            
            if (companyId.HasValue)
                existingQuery = existingQuery.Where(a => a.CompanyId == companyId.Value);
            else
                existingQuery = existingQuery.Where(a => a.CompanyId == null);

            var existingFileNames = await existingQuery
                .Select(a => a.FileName.ToLower())
                .ToListAsync();

            var uploadedAssets = new List<Asset>();
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "assets", userId);
            var thumbnailsPath = Path.Combine(_environment.WebRootPath, "uploads", "thumbnails", userId);

            // Create directories if they don't exist
            Directory.CreateDirectory(uploadsPath);
            Directory.CreateDirectory(thumbnailsPath);

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                try
                {
                    // Calculate file checksum
                    string? checksum = null;
                    using (var stream = file.OpenReadStream())
                    {
                        checksum = await ComputeFileChecksumAsync(stream);
                    }

                    // Generate unique filename if duplicate exists
                    var originalFileName = Path.GetFileName(file.FileName);
                    var uniqueFileName = GetUniqueFileName(originalFileName, existingFileNames);
                    
                    // Add to tracking list for subsequent files in same batch
                    existingFileNames.Add(uniqueFileName.ToLower());

                    var physicalFileName = $"{Guid.NewGuid()}_{uniqueFileName}";
                    var filePath = Path.Combine(uploadsPath, physicalFileName);

                    // Save file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Determine file type
                    var fileType = GetFileType(file.ContentType, uniqueFileName);

                    string? thumbnailPath = null;
                    string? iptcMetadata = null;

                    if (fileType == "image" || fileType == "video")
                    {
                        thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                            filePath,
                            thumbnailsPath,
                            fileType,
                            file.ContentType);
                    }

                    if (fileType == "image")
                    {
                        iptcMetadata = await _iptcExtractionService.ExtractIptcMetadataAsync(filePath);
                    }

                    var asset = new Asset
                    {
                        Id = Guid.NewGuid(),
                        FileName = uniqueFileName,
                        FilePath = filePath,
                        FileType = fileType,
                        MimeType = file.ContentType,
                        FileSize = file.Length,
                        FileChecksum = checksum,
                        ThumbnailPath = thumbnailPath,
                        IptcMetadata = iptcMetadata,
                        FolderId = folderId,
                        UserId = userId,
                        CompanyId = companyId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _assetRepo.AddAsync(asset);
                    uploadedAssets.Add(asset);

                    _logger.LogInformation("File {FileName} uploaded by user {UserId} (thumbnail: {HasThumbnail})", uniqueFileName, userId, thumbnailPath != null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
                }
            }

            await _assetRepo.SaveAsync();

            // Update folder asset count
            if (folderId.HasValue)
            {
                var folder = await _folderRepo
                    .Query()
                    .FirstOrDefaultAsync(f => f.Id == folderId.Value);
                    
                if (folder != null)
                {
                    folder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == folderId.Value);
                    _folderRepo.Update(folder);
                    await _folderRepo.SaveAsync();
                }
            }

            return _mapper.Map<IEnumerable<AssetDto>>(uploadedAssets);
        }

        public async Task<AssetDto> UpdateAssetAsync(Guid assetId, string userId, Guid? companyId, UpdateAssetDto dto)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId);

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var asset = await query.Include(a => a.User).FirstOrDefaultAsync();

            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found or access denied for user {UserId}", assetId, userId);
                return null;
            }

            asset.FileName = dto.Name;
            asset.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(dto.UserMetadata))
            {
                asset.UserMetadata = dto.UserMetadata;
            }

            if (asset.FolderId != dto.FolderId)
            {
                var oldFolderId = asset.FolderId;
                
                if (dto.FolderId.HasValue)
                {
                    var newFolder = await _folderRepo
                        .Query()
                        .FirstOrDefaultAsync(f => f.Id == dto.FolderId.Value && f.UserId == userId);

                    if (newFolder == null)
                    {
                        _logger.LogWarning("Folder {FolderId} not found or access denied for user {UserId}", dto.FolderId.Value, userId);
                        return null;
                    }
                }

                asset.FolderId = dto.FolderId;

                if (oldFolderId.HasValue)
                {
                    var oldFolder = await _folderRepo.Query().FirstOrDefaultAsync(f => f.Id == oldFolderId.Value);
                    if (oldFolder != null)
                    {
                        oldFolder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == oldFolderId.Value);
                        _folderRepo.Update(oldFolder);
                    }
                }

                if (dto.FolderId.HasValue)
                {
                    var newFolder = await _folderRepo.Query().FirstOrDefaultAsync(f => f.Id == dto.FolderId.Value);
                    if (newFolder != null)
                    {
                        newFolder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == dto.FolderId.Value);
                        _folderRepo.Update(newFolder);
                    }
                }
                
                await _folderRepo.SaveAsync();
            }

            _assetRepo.Update(asset);
            await _assetRepo.SaveAsync();

            _logger.LogInformation("Asset {AssetId} updated by user {UserId}", assetId, userId);

            return _mapper.Map<AssetDto>(asset);
        }

        public async Task<int> UpdateAssetsMetadataAsync(List<Guid> assetIds, string userId, Guid? companyId, string key, string value)
        {
            var query = _assetRepo.Query().Where(a => assetIds.Contains(a.Id));

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var assets = await query.ToListAsync();

            if (!assets.Any()) return 0;

            int count = 0;
            foreach (var asset in assets)
            {
                Dictionary<string, string> metadata = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(asset.UserMetadata))
                {
                    try
                    {
                        metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(asset.UserMetadata) 
                                   ?? new Dictionary<string, string>();
                    }
                    catch
                    {
                        // If JSON is invalid, start with empty dictionary
                        _logger.LogWarning("Malformed metadata for asset {AssetId}", asset.Id);
                    }
                }

                if (string.IsNullOrEmpty(value))
                {
                    if (metadata.ContainsKey(key))
                    {
                        metadata.Remove(key);
                    }
                }
                else
                {
                    metadata[key] = value;
                }

                asset.UserMetadata = System.Text.Json.JsonSerializer.Serialize(metadata);
                asset.UpdatedAt = DateTime.UtcNow;
                _assetRepo.Update(asset);
                count++;
            }

            await _assetRepo.SaveAsync();
            _logger.LogInformation("Updated metadata '{Key}' for {Count} assets by user {UserId}", key, count, userId);
            
            return count;
        }

        public async Task<bool> MoveAssetAsync(Guid assetId, Guid newFolderId, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId);
            
            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var asset = await query.FirstOrDefaultAsync();

            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for move by user {UserId}", assetId, userId);
                return false;
            }

            var newFolder = await _folderRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == newFolderId && f.UserId == userId);

            if (newFolder == null)
            {
                _logger.LogWarning("Folder {FolderId} not found or access denied for user {UserId}", newFolderId, userId);
                return false;
            }

            var oldFolderId = asset.FolderId;
            asset.FolderId = newFolderId;
            asset.UpdatedAt = DateTime.UtcNow;

            _assetRepo.Update(asset);
            await _assetRepo.SaveAsync();

            if (oldFolderId.HasValue)
            {
                var oldFolder = await _folderRepo.Query().FirstOrDefaultAsync(f => f.Id == oldFolderId.Value);
                if (oldFolder != null)
                {
                    oldFolder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == oldFolderId.Value);
                    _folderRepo.Update(oldFolder);
                }
            }

            newFolder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == newFolderId);
            _folderRepo.Update(newFolder);
            await _folderRepo.SaveAsync();

            _logger.LogInformation("Asset {AssetId} moved to folder {FolderId} by user {UserId}", assetId, newFolderId, userId);

            return true;
        }

        public async Task<bool> DeleteAssetAsync(Guid assetId, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId);

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var asset = await query.FirstOrDefaultAsync();

            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for deletion by user {UserId}", assetId, userId);
                return false;
            }

            // Soft delete
            asset.IsDeleted = true;
            asset.DeletedAt = DateTime.UtcNow;

            var folderId = asset.FolderId;

            _assetRepo.Update(asset);
            await _assetRepo.SaveAsync();

            // Update folder asset count
            if (folderId.HasValue)
            {
                var folder = await _folderRepo
                    .Query()
                    .FirstOrDefaultAsync(f => f.Id == folderId.Value);
                    
                if (folder != null)
                {
                    folder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == folderId.Value && !a.IsDeleted);
                    _folderRepo.Update(folder);
                    await _folderRepo.SaveAsync();
                }
            }

            _logger.LogInformation("Asset {AssetId} soft-deleted by user {UserId}", assetId, userId);

            return true;
        }

        public async Task<bool> DeleteAssetsAsync(List<Guid> assetIds, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => assetIds.Contains(a.Id));

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var assets = await query.ToListAsync();

            if (!assets.Any())
            {
                _logger.LogWarning("No assets found for batch deletion by user {UserId}", userId);
                return false;
            }

            var folderIds = assets.Where(a => a.FolderId.HasValue).Select(a => a.FolderId.Value).Distinct().ToList();

            foreach (var asset in assets)
            {
                // Soft delete
                asset.IsDeleted = true;
                asset.DeletedAt = DateTime.UtcNow;
                _assetRepo.Update(asset);
            }

            await _assetRepo.SaveAsync();

            // Update folder asset counts
            foreach (var folderId in folderIds)
            {
                var folder = await _folderRepo
                    .Query()
                    .FirstOrDefaultAsync(f => f.Id == folderId);
                    
                if (folder != null)
                {
                    folder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == folderId && !a.IsDeleted);
                    _folderRepo.Update(folder);
                }
            }

            await _folderRepo.SaveAsync();

            _logger.LogInformation("{Count} assets soft-deleted by user {UserId}", assets.Count, userId);

            return true;
        }

        public async Task<byte[]> DownloadAssetAsync(Guid assetId, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId);

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.CompanyId == null);

            var asset = await query.FirstOrDefaultAsync();

            if (asset == null || !File.Exists(asset.FilePath))
            {
                _logger.LogWarning("Asset {AssetId} not found or file missing for user {UserId}", assetId, userId);
                return null;
            }

            return await File.ReadAllBytesAsync(asset.FilePath);
        }

        public async Task<byte[]> DownloadAssetsAsZipAsync(List<Guid> assetIds, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => assetIds.Contains(a.Id) && !a.IsDeleted);

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.CompanyId == null);

            var assets = await query.ToListAsync();

            if (!assets.Any())
            {
                _logger.LogWarning("No assets found for bulk download by user {UserId}", userId);
                return null;
            }

            using var memoryStream = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                var fileNameCounts = new Dictionary<string, int>();

                foreach (var asset in assets)
                {
                    if (!File.Exists(asset.FilePath))
                    {
                        _logger.LogWarning("Asset file {FilePath} not found, skipping", asset.FilePath);
                        continue;
                    }

                    try
                    {
                        var fileName = asset.FileName;
                        
                        if (fileNameCounts.ContainsKey(fileName.ToLower()))
                        {
                            fileNameCounts[fileName.ToLower()]++;
                            var extension = Path.GetExtension(fileName);
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            fileName = $"{nameWithoutExt}_{fileNameCounts[fileName.ToLower()]}{extension}";
                        }
                        else
                        {
                            fileNameCounts[fileName.ToLower()] = 1;
                        }

                        var entry = archive.CreateEntry(fileName, System.IO.Compression.CompressionLevel.Fastest);
                        using var entryStream = entry.Open();
                        using var fileStream = new FileStream(asset.FilePath, FileMode.Open, FileAccess.Read);
                        await fileStream.CopyToAsync(entryStream);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding asset {AssetId} to ZIP", asset.Id);
                    }
                }
            }

            _logger.LogInformation("{Count} assets downloaded as ZIP by user {UserId}", assets.Count, userId);
            return memoryStream.ToArray();
        }


        public async Task<PagedAssetResultDto> SearchAssetsAsync(string userId, Guid? companyId, Guid? folderId = null, string? fileType = null, string? keyword = null, string sortBy = "date", string sortDir = "desc", int page = 1, int? pageSize = null, List<Guid>? assetIds = null)
        {
            _logger.LogInformation("SearchAssetsAsync called - UserId: {UserId}, CompanyId: {CompanyId}, FolderId: {FolderId}, Keyword: {Keyword}, IdsCount: {IdsCount}", userId, companyId, folderId, keyword, assetIds?.Count ?? 0);
            
            IQueryable<Asset> query = _assetRepo.Query();
            
            bool needsTags = !string.IsNullOrEmpty(keyword);
            if (needsTags)
            {
                query = query.Include(a => a.AssetTags).ThenInclude(at => at.VisualTag);
            }

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value && !a.IsDeleted);
            else
                query = query.Where(a => a.CompanyId == null && !a.IsDeleted);

            if (assetIds != null && assetIds.Count > 0)
            {
                query = query.Where(a => assetIds.Contains(a.Id));
            }

            // Add folder filtering
            if (folderId.HasValue)
            {
                query = query.Where(a => a.FolderId == folderId.Value);
            }



            if (!string.IsNullOrEmpty(fileType))
            {
                query = query.Where(a => a.FileType == fileType);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                var searchPattern = $"%{keyword}%";
                var metadataExactPattern = $"%:\"{EscapeLike(keyword)}\"%";
                _logger.LogInformation("Applying search with pattern: {Pattern}, metadata exact pattern: {MetadataPattern}", searchPattern, metadataExactPattern);
                
                query = query.Where(a => 
                    EF.Functions.Like(a.FileName, searchPattern) ||
                    a.AssetTags.Any(at => EF.Functions.Like(at.VisualTag.Name, searchPattern)) ||
                    (a.UserMetadata != null && EF.Functions.Like(a.UserMetadata, metadataExactPattern))
                );
            }

            var total = await query.CountAsync();
            _logger.LogInformation("Total results found: {Total}", total);

            query = ApplySorting(query, sortBy, sortDir);

            if (pageSize.HasValue && pageSize.Value > 0)
            {
                var skip = (page - 1) * pageSize.Value;
                query = query.Skip(skip).Take(pageSize.Value);
            }

            var assets = await query.ToListAsync();
            
            if (assets.Any())
            {

            }

            return new PagedAssetResultDto
            {
                Assets = _mapper.Map<IEnumerable<AssetDto>>(assets),
                Total = total,
                Page = page,
                HasMore = pageSize.HasValue && (page * pageSize.Value) < total
            };
        }

        public async Task<PagedAssetResultDto> AdvancedSearchAssetsAsync(string userId, Guid? companyId, AdvancedSearchRequestDto request)
        {
            var normalizedRequest = request ?? new AdvancedSearchRequestDto();
            
            _logger.LogInformation("AdvancedSearchAssetsAsync called by {UserId}. Conditions: {ConditionCount}. Logic: {Logic}", 
                userId, normalizedRequest.Conditions?.Count ?? 0, normalizedRequest.Logic);

            if (normalizedRequest.Conditions != null)
            {
                foreach (var c in normalizedRequest.Conditions)
                {
                    _logger.LogInformation("Condition: Field={Field}, Op={Op}, Val={Val}, SecVal={SecVal}", c.Field, c.Operator, c.Value, c.SecondaryValue);
                }
            }

            var query = _assetRepo.Query();

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value && !a.IsDeleted);
            else
                query = query.Where(a => a.CompanyId == null && !a.IsDeleted);
            
            if (normalizedRequest.FolderId.HasValue)
            {
                query = query.Where(a => a.FolderId == normalizedRequest.FolderId.Value);
            }

            // Fix: Log the number of predicates generated
            var predicates = BuildConditionPredicates(normalizedRequest.Conditions).ToList();
            _logger.LogInformation("Generated {PredicateCount} predicates from conditions", predicates.Count);

            if (predicates.Count > 0)
            {
                var useOr = string.Equals(normalizedRequest.Logic, "OR", StringComparison.OrdinalIgnoreCase);
                
                var combined = useOr ? PredicateBuilder.False<Asset>() : PredicateBuilder.True<Asset>();
                
                foreach (var predicate in predicates)
                {
                    combined = useOr ? combined.Or(predicate) : combined.And(predicate);
                }
                
                query = query.Where(combined);
            }
            else if (normalizedRequest.Conditions != null && normalizedRequest.Conditions.Count > 0 && normalizedRequest.Logic == "AND")
            {
                 // If conditions were provided but no predicates were built (e.g. parsing failed), 
                 // and logic is AND, we probably shouldn't return EVERYTHING if the user intended to filter.
                 // However, current logic behaves this way. 
                 _logger.LogWarning("Conditions provided but no predicates built. Returning all assets (filtered by folder/company).");
            }

            query = ApplySorting(query, normalizedRequest.SortBy, normalizedRequest.SortDir);

            var page = normalizedRequest.Page > 0 ? normalizedRequest.Page : 1;
            int? pageSize = normalizedRequest.PageSize.HasValue && normalizedRequest.PageSize.Value > 0
                ? normalizedRequest.PageSize
                : null;

            var total = await query.CountAsync();

            if (pageSize.HasValue)
            {
                var skip = (page - 1) * pageSize.Value;
                query = query.Skip(skip).Take(pageSize.Value);
            }

            var assets = await query.ToListAsync();

            return new PagedAssetResultDto
            {
                Assets = _mapper.Map<IEnumerable<AssetDto>>(assets),
                Total = total,
                Page = page,
                HasMore = pageSize.HasValue && (page * pageSize.Value) < total
            };
        }

        public async Task<PagedAssetResultDto> GetDeletedAssetsAsync(string userId, Guid? companyId, string sortBy = "date", string sortDir = "desc", int page = 1, int? pageSize = null)
        {
            var query = _assetRepo.Query();

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value && a.IsDeleted);
            else
                query = query.Where(a => a.CompanyId == null && a.IsDeleted);

            var total = await query.CountAsync();

            query = ApplySorting(query, sortBy ?? "deletedAt", sortDir);

            if (pageSize.HasValue && pageSize.Value > 0)
            {
                var skip = (page - 1) * pageSize.Value;
                query = query.Skip(skip).Take(pageSize.Value);
            }

            var assets = await query.ToListAsync();

            return new PagedAssetResultDto
            {
                Assets = _mapper.Map<IEnumerable<AssetDto>>(assets),
                Total = total,
                Page = page,
                HasMore = pageSize.HasValue && (page * pageSize.Value) < total
            };
        }

        public async Task<bool> RestoreAssetAsync(Guid assetId, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId && a.IsDeleted);

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var asset = await query.FirstOrDefaultAsync();

            if (asset == null)
            {
                return false;
            }

            asset.IsDeleted = false;
            asset.DeletedAt = null;

            var folderId = asset.FolderId;
            _assetRepo.Update(asset);
            await _assetRepo.SaveAsync();

            if (folderId.HasValue)
            {
                var folder = await _folderRepo.Query().FirstOrDefaultAsync(f => f.Id == folderId.Value);
                if (folder != null)
                {
                    folder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == folderId.Value && !a.IsDeleted);
                    _folderRepo.Update(folder);
                    await _folderRepo.SaveAsync();
                }
            }

            _logger.LogInformation("Asset {AssetId} restored by user {UserId}", assetId, userId);
            return true;
        }

        public async Task<bool> RestoreAssetsAsync(List<Guid> assetIds, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => assetIds.Contains(a.Id) && a.IsDeleted);

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var assets = await query.ToListAsync();

            if (!assets.Any())
            {
                return false;
            }

            foreach (var asset in assets)
            {
                asset.IsDeleted = false;
                asset.DeletedAt = null;
                _assetRepo.Update(asset);
            }

            await _assetRepo.SaveAsync();

            var folderIds = assets.Where(a => a.FolderId.HasValue).Select(a => a.FolderId.Value).Distinct();
            foreach (var folderId in folderIds)
            {
                var folder = await _folderRepo.Query().FirstOrDefaultAsync(f => f.Id == folderId);
                if (folder != null)
                {
                    folder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == folderId && !a.IsDeleted);
                    _folderRepo.Update(folder);
                }
            }
            await _folderRepo.SaveAsync();

            _logger.LogInformation("{Count} assets restored by user {UserId}", assets.Count, userId);
            return true;
        }

        public async Task<bool> PermanentlyDeleteAssetAsync(Guid assetId, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.Id == assetId && a.IsDeleted);

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);
            
            var asset = await query.FirstOrDefaultAsync();

            if (asset == null)
            {
                return false;
            }

            if (File.Exists(asset.FilePath)) File.Delete(asset.FilePath);
            if (!string.IsNullOrEmpty(asset.ThumbnailPath) && File.Exists(asset.ThumbnailPath)) File.Delete(asset.ThumbnailPath);

            _assetRepo.Delete(asset);
            await _assetRepo.SaveAsync();

            _logger.LogInformation("Asset {AssetId} permanently deleted by user {UserId}", assetId, userId);
            return true;
        }

        public async Task<bool> PermanentlyDeleteAssetsAsync(List<Guid> assetIds, string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => assetIds.Contains(a.Id) && a.IsDeleted);

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var assets = await query.ToListAsync();

            if (!assets.Any()) return false;

            foreach (var asset in assets)
            {
                if (File.Exists(asset.FilePath)) File.Delete(asset.FilePath);
                if (!string.IsNullOrEmpty(asset.ThumbnailPath) && File.Exists(asset.ThumbnailPath)) File.Delete(asset.ThumbnailPath);
                _assetRepo.Delete(asset);
            }

            await _assetRepo.SaveAsync();
            _logger.LogInformation("{Count} assets permanently deleted by user {UserId}", assets.Count, userId);
            return true;
        }

        public async Task<int> PermanentlyDeleteExpiredAssetsAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            
            var expiredAssets = await _assetRepo
                .Query()
                .Where(a => a.IsDeleted && a.DeletedAt != null && a.DeletedAt < cutoffDate)
                .ToListAsync();

            if (!expiredAssets.Any()) return 0;

            foreach (var asset in expiredAssets)
            {
                try
                {
                    if (File.Exists(asset.FilePath)) File.Delete(asset.FilePath);
                    if (!string.IsNullOrEmpty(asset.ThumbnailPath) && File.Exists(asset.ThumbnailPath)) File.Delete(asset.ThumbnailPath);
                    _assetRepo.Delete(asset);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error permanently deleting expired asset {AssetId}", asset.Id);
                }
            }

            await _assetRepo.SaveAsync();
            return expiredAssets.Count;
        }

        private IQueryable<Asset> ApplySorting(IQueryable<Asset> query, string? sortBy, string? sortDir)
        {
            var sortField = (sortBy ?? "date").Trim();
            var normalizedDir = (sortDir ?? "desc").Trim().ToLowerInvariant() == "asc" ? "asc" : "desc";
            var isAsc = normalizedDir == "asc";

            if (sortField.StartsWith("Metadata.", StringComparison.OrdinalIgnoreCase))
            {
                var metadataKey = sortField.Substring(9).Trim();
                if (!string.IsNullOrEmpty(metadataKey))
                {
                    var path = $"$.\"{metadataKey}\"";
                    
                    // Explicitly handle null UserMetadata to help translation
                    if (isAsc)
                        return query.OrderBy(a => EF.Functions.JsonExtract<string>(a.UserMetadata, path));
                    else
                        return query.OrderByDescending(a => EF.Functions.JsonExtract<string>(a.UserMetadata, path));
                }
            }

            var normalizedSort = sortField.ToLowerInvariant();

            return (normalizedSort) switch
            {
                "name" => isAsc ? query.OrderBy(a => a.FileName) : query.OrderByDescending(a => a.FileName),
                "size" => isAsc ? query.OrderBy(a => a.FileSize) : query.OrderByDescending(a => a.FileSize),
                "type" => isAsc ? query.OrderBy(a => a.FileType) : query.OrderByDescending(a => a.FileType),
                "deletedat" => isAsc ? query.OrderBy(a => a.DeletedAt) : query.OrderByDescending(a => a.DeletedAt),
                _ => isAsc ? query.OrderBy(a => a.CreatedAt) : query.OrderByDescending(a => a.CreatedAt),
            };
        }

        private IEnumerable<Expression<Func<Asset, bool>>> BuildConditionPredicates(IEnumerable<AdvancedSearchConditionDto>? conditions)
        {
            if (conditions == null)
            {
                yield break;
            }

            foreach (var condition in conditions)
            {
                var predicate = BuildConditionPredicate(condition);
                if (predicate != null)
                {
                    yield return predicate;
                }
            }
        }

        private Expression<Func<Asset, bool>>? BuildConditionPredicate(AdvancedSearchConditionDto condition)
        {
            if (condition == null)
            {
                return null;
            }

            var fieldKey = NormalizeField(condition.Field);
            
            return fieldKey switch
            {
                "filename" => BuildStringCondition(nameof(Asset.FileName), condition),
                "filetype" => condition.Values != null && condition.Values.Count > 0
                    ? BuildStringListCondition(nameof(Asset.FileType), condition.Values)
                    : BuildStringCondition(nameof(Asset.FileType), condition),
                "tags" => BuildTagCondition(condition),
                "filesize" => BuildFileSizeCondition(condition),
                "datecreated" => BuildDateCondition(condition),
                "createdat" => BuildDateCondition(condition),
                "metadata" => BuildMetadataCondition(condition),
                "id" => BuildIdCondition(condition),
                "ids" => BuildIdCondition(condition),
                _ => null
            };
        }

        private Expression<Func<Asset, bool>>? BuildStringCondition(string propertyName, AdvancedSearchConditionDto condition)
        {
            var value = condition.Value?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var op = NormalizeOperator(condition.Operator);
            var param = Expression.Parameter(typeof(Asset), "asset");
            var member = Expression.Property(param, propertyName);
            Expression comparison = op switch
            {
                "contains" => BuildLikeExpression(member, $"%{EscapeLike(value)}%"),
                "startswith" => BuildLikeExpression(member, $"{EscapeLike(value)}%"),
                "endswith" => BuildLikeExpression(member, $"%{EscapeLike(value)}"),
                _ => Expression.Equal(member, Expression.Constant(value))
            };

            var notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
            var body = Expression.AndAlso(notNull, comparison);

            return Expression.Lambda<Func<Asset, bool>>(body, param);
        }

        private Expression<Func<Asset, bool>>? BuildStringListCondition(string propertyName, IEnumerable<string>? values)
        {
            var normalized = values?
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (normalized == null || normalized.Count == 0)
            {
                return null;
            }

            var param = Expression.Parameter(typeof(Asset), "asset");
            var member = Expression.Property(param, propertyName);
            var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var toLower = Expression.Call(member, toLowerMethod);
            var contains = Expression.Call(Expression.Constant(normalized), StringListContainsMethod!, toLower);
            var notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
            var body = Expression.AndAlso(notNull, contains);
            return Expression.Lambda<Func<Asset, bool>>(body, param);
        }

        private Expression<Func<Asset, bool>>? BuildMetadataCondition(AdvancedSearchConditionDto condition)
        {
            var key = condition.MetadataField?.Trim();
            var value = condition.Value?.Trim();
            var op = NormalizeOperator(condition.Operator);

            if (string.IsNullOrEmpty(key)) return null;

            var jsonPath = $"$.\"{key}\"";

            if (op == "isuntagged")
            {
                // Simple LIKE check for the key's existence in the JSON string
                // Note: This is an approximation but very effective for simple JSON metadata
                var keySearch = $"\"{key}\":";
                return a => string.IsNullOrEmpty(a.UserMetadata) || !EF.Functions.Like(a.UserMetadata, $"%\"{key}\":%", "\\");
            }

            if (string.IsNullOrEmpty(value)) return null;
            var escaped = EscapeLike(value);

            switch (op)
            {
                case "is":
                case "equals":
                    return a => a.UserMetadata != null && EF.Functions.Like(a.UserMetadata, $"%\"{key}\":\"{escaped}\"%", "\\");
                case "isnot":
                    return a => a.UserMetadata != null && !EF.Functions.Like(a.UserMetadata, $"%\"{key}\":\"{escaped}\"%", "\\") && EF.Functions.Like(a.UserMetadata, $"%\"{key}\":%", "\\");
                case "startswith":
                    return a => a.UserMetadata != null && EF.Functions.Like(a.UserMetadata, $"%\"{key}\":\"{escaped}%\"%", "\\");
                case "endswith":
                    return a => a.UserMetadata != null && EF.Functions.Like(a.UserMetadata, $"%\"{key}\":\"%{escaped}\"%", "\\");
                case "contains":
                default:
                    return a => a.UserMetadata != null && EF.Functions.Like(a.UserMetadata, $"%\"{key}\":\"%{escaped}%\"%", "\\");
            }
        }

        private Expression<Func<Asset, bool>>? BuildIdCondition(AdvancedSearchConditionDto condition)
        {
            var ids = new List<Guid>();

            if (condition.Values != null && condition.Values.Any())
            {
                foreach (var val in condition.Values)
                {
                    if (Guid.TryParse(val, out var guid))
                    {
                        ids.Add(guid);
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(condition.Value) && Guid.TryParse(condition.Value, out var singleGuid))
            {
                ids.Add(singleGuid);
            }

            if (ids.Count == 0) return null;

            return a => ids.Contains(a.Id);
        }

        private Expression<Func<Asset, bool>>? BuildTagCondition(AdvancedSearchConditionDto condition)
        {
            if (condition.Values == null || condition.Values.Count == 0)
            {
                return null;
            }

            var tagIds = condition.Values
                .Select(v => Guid.TryParse(v, out var guid) ? guid : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .Distinct()
                .ToList();

            if (tagIds.Count == 0)
            {
                return null;
            }

            var op = NormalizeOperator(condition.Operator);
            var param = Expression.Parameter(typeof(Asset), "asset");
            var assetTagsProperty = Expression.Property(param, nameof(Asset.AssetTags));

            if (op == "containsall")
            {
                Expression? combined = null;
                foreach (var tagId in tagIds)
                {
                    var tagParam = Expression.Parameter(typeof(AssetTag), "t");
                    var equality = Expression.Equal(Expression.Property(tagParam, nameof(AssetTag.VisualTagId)), Expression.Constant(tagId));
                    var tagLambda = Expression.Lambda<Func<AssetTag, bool>>(equality, tagParam);
                    var anyCall = Expression.Call(AnyAssetTagMethod, assetTagsProperty, tagLambda);
                    combined = combined == null ? anyCall : Expression.AndAlso(combined, anyCall);
                }

                if (combined == null)
                {
                    return null;
                }

                return Expression.Lambda<Func<Asset, bool>>(combined, param);
            }
            else
            {
                var tagParam = Expression.Parameter(typeof(AssetTag), "t");
                var visualTagId = Expression.Property(tagParam, nameof(AssetTag.VisualTagId));
                var containsCall = Expression.Call(Expression.Constant(tagIds), GuidListContainsMethod!, visualTagId);
                var tagLambda = Expression.Lambda<Func<AssetTag, bool>>(containsCall, tagParam);
                var anyCall = Expression.Call(AnyAssetTagMethod, assetTagsProperty, tagLambda);
                return Expression.Lambda<Func<Asset, bool>>(anyCall, param);
            }
        }

        private Expression<Func<Asset, bool>>? BuildFileSizeCondition(AdvancedSearchConditionDto condition)
        {
            var op = NormalizeOperator(condition.Operator);
            var param = Expression.Parameter(typeof(Asset), "asset");
            var member = Expression.Property(param, nameof(Asset.FileSize));
            Expression? body = null;

            switch (op)
            {
                case "greaterthan":
                    var greaterThanValue = ConvertToBytes(condition.Value, condition.Unit);
                    if (greaterThanValue.HasValue)
                    {
                        body = Expression.GreaterThan(member, Expression.Constant(greaterThanValue.Value));
                    }
                    break;
                case "lessthan":
                    var lessThanValue = ConvertToBytes(condition.Value, condition.Unit);
                    if (lessThanValue.HasValue)
                    {
                        body = Expression.LessThan(member, Expression.Constant(lessThanValue.Value));
                    }
                    break;
                case "between":
                    var range = MergeRange(condition);
                    var from = ConvertToBytes(range?.From, condition.Unit);
                    var to = ConvertToBytes(range?.To, condition.Unit);
                    if (from.HasValue && to.HasValue)
                    {
                        var lower = Math.Min(from.Value, to.Value);
                        var upper = Math.Max(from.Value, to.Value);
                        body = Expression.AndAlso(
                            Expression.GreaterThanOrEqual(member, Expression.Constant(lower)),
                            Expression.LessThanOrEqual(member, Expression.Constant(upper)));
                    }
                    break;
                default:
                    var equalsValue = ConvertToBytes(condition.Value, condition.Unit);
                    if (equalsValue.HasValue)
                    {
                        body = Expression.Equal(member, Expression.Constant(equalsValue.Value));
                    }
                    break;
            }

            if (body == null)
            {
                return null;
            }

            return Expression.Lambda<Func<Asset, bool>>(body, param);
        }

        private Expression<Func<Asset, bool>>? BuildDateCondition(AdvancedSearchConditionDto condition)
        {
            var op = NormalizeOperator(condition.Operator);
            var param = Expression.Parameter(typeof(Asset), "asset");
            var member = Expression.Property(param, nameof(Asset.CreatedAt));
            Expression? body = null;

            switch (op)
            {
                case "after":
                    var afterValue = ParseDate(condition.Value);
                    if (afterValue.HasValue)
                    {
                        body = Expression.GreaterThanOrEqual(member, Expression.Constant(afterValue.Value));
                    }
                    break;
                case "before":
                    var beforeValue = ParseDate(condition.Value, endOfDay: true);
                    if (beforeValue.HasValue)
                    {
                        body = Expression.LessThanOrEqual(member, Expression.Constant(beforeValue.Value));
                    }
                    break;
                case "between":
                    var range = MergeRange(condition);
                    var from = ParseDate(range?.From);
                    var to = ParseDate(range?.To, endOfDay: true);
                    if (from.HasValue && to.HasValue)
                    {
                        var start = from.Value <= to.Value ? from.Value : to.Value;
                        var end = from.Value <= to.Value ? to.Value : from.Value;
                        body = Expression.AndAlso(
                            Expression.GreaterThanOrEqual(member, Expression.Constant(start)),
                            Expression.LessThanOrEqual(member, Expression.Constant(end)));
                    }
                    else if (from.HasValue)
                    {
                        body = Expression.GreaterThanOrEqual(member, Expression.Constant(from.Value));
                    }
                    else if (to.HasValue)
                    {
                        body = Expression.LessThanOrEqual(member, Expression.Constant(to.Value));
                    }
                    break;
                case "on":
                    var day = ParseDate(condition.Value);
                    if (day.HasValue)
                    {
                        var startOfDay = day.Value.Date;
                        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                        body = Expression.AndAlso(
                            Expression.GreaterThanOrEqual(member, Expression.Constant(startOfDay)),
                            Expression.LessThanOrEqual(member, Expression.Constant(endOfDay)));
                    }
                    break;
                default:
                    var exact = ParseDate(condition.Value);
                    if (exact.HasValue)
                    {
                        body = Expression.Equal(member, Expression.Constant(exact.Value));
                    }
                    break;
            }

            if (body == null)
            {
                return null;
            }

            return Expression.Lambda<Func<Asset, bool>>(body, param);
        }

        private Expression BuildLikeExpression(Expression member, string pattern)
        {
            var functions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
            return Expression.Call(LikeMethod, functions, member, Expression.Constant(pattern), Expression.Constant("\\"));
        }

        private static string EscapeLike(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        private static string NormalizeField(string? field)
        {
            return (field ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static string NormalizeOperator(string? op)
        {
            return (op ?? "equals").Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static long? ConvertToBytes(string? value, string? unit)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return null;
            }

            var multiplier = (unit ?? "bytes").Trim().ToLowerInvariant() switch
            {
                "gb" => 1024d * 1024d * 1024d,
                "mb" => 1024d * 1024d,
                "kb" => 1024d,
                _ => 1d
            };

            var bytes = parsed * multiplier;
            if (bytes < 0)
            {
                return null;
            }

            return (long)Math.Round(bytes);
        }

        private static DateTime? ParseDate(string? value, bool endOfDay = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // 1. Try strict yyyy-MM-dd parsing first (typical HTML input) -> Treat as UTC date
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                var utc = DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
                if (endOfDay)
                {
                    utc = utc.AddDays(1).AddTicks(-1);
                }
                return utc;
            }

            // 2. Fallback to flexible parsing
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                if (!DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed))
                {
                    return null;
                }
            }

            var result = parsed.Kind == DateTimeKind.Utc ? parsed : DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();

            if (endOfDay)
            {
                result = result.Date.AddDays(1).AddTicks(-1);
            }

            return result;
        }

        private static AdvancedRangeDto? MergeRange(AdvancedSearchConditionDto condition)
        {
            if (condition == null)
            {
                return null;
            }

            if (condition.Range != null)
            {
                return condition.Range;
            }

            if (condition.Values != null && condition.Values.Count >= 2)
            {
                return new AdvancedRangeDto
                {
                    From = condition.Values[0],
                    To = condition.Values[1]
                };
            }

            if (!string.IsNullOrWhiteSpace(condition.Value) || !string.IsNullOrWhiteSpace(condition.SecondaryValue))
            {
                return new AdvancedRangeDto
                {
                    From = condition.Value,
                    To = condition.SecondaryValue
                };
            }

            return null;
        }

        private string GetUniqueFileName(string fileName, List<string> existingFileNames)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var uniqueName = fileName;
            var counter = 1;

            while (existingFileNames.Contains(uniqueName.ToLower()))
            {
                uniqueName = $"{nameWithoutExt} ({counter}){extension}";
                counter++;
            }

            return uniqueName;
        }

        private string GetFileType(string mimeType, string fileName = "")
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (ImageExtensions.Contains(extension))
            {
                return "image";
            }

            if (VideoExtensions.Contains(extension))
            {
                return "video";
            }

            if (AudioExtensions.Contains(extension))
            {
                return "audio";
            }

            if (mimeType.StartsWith("image/")) return "image";
            if (mimeType.StartsWith("video/")) return "video";
            if (mimeType.StartsWith("audio/")) return "audio";
            if (mimeType.Contains("pdf")) return "document";
            if (mimeType.Contains("word") || mimeType.Contains("document")) return "document";
            if (mimeType.Contains("sheet") || mimeType.Contains("excel")) return "document";
            if (mimeType.Contains("zip") || mimeType.Contains("rar") || mimeType.Contains("7z")) return "archive";
            return "other";
        }

        public async Task<int> ExtractIptcForExistingAssetsAsync(string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => a.FileType == "image" && (a.IptcMetadata == null || a.IptcMetadata == ""));

            if (companyId.HasValue)
                query = query.Where(a => a.UserId == userId || a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            var imageAssets = await query.ToListAsync();

            var count = 0;

            foreach (var asset in imageAssets)
            {
                if (!File.Exists(asset.FilePath))
                {
                    _logger.LogWarning("Asset file not found: {FilePath}", asset.FilePath);
                    continue;
                }

                var iptcMetadata = await _iptcExtractionService.ExtractIptcMetadataAsync(asset.FilePath);

                if (!string.IsNullOrEmpty(iptcMetadata))
                {
                    asset.IptcMetadata = iptcMetadata;
                    asset.UpdatedAt = DateTime.UtcNow;
                    _assetRepo.Update(asset);
                    count++;
                }
            }

            if (count > 0)
            {
                await _assetRepo.SaveAsync();
            }

            _logger.LogInformation("Extracted IPTC metadata for {Count} assets", count);
            return count;
        }

        public async Task<int> GetTotalAssetCountAsync(string userId, Guid? companyId)
        {
            var query = _assetRepo.Query().Where(a => !a.IsDeleted);

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value);
            else
                query = query.Where(a => a.UserId == userId);

            return await query.CountAsync();
        }

        private async Task<string> ComputeFileChecksumAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            stream.Position = 0;
            var hash = await sha256.ComputeHashAsync(stream);
            stream.Position = 0;
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public async Task<DuplicateCheckResultDto> CheckForDuplicatesAsync(IFormFileCollection files, string userId, Guid? companyId)
        {
            var result = new DuplicateCheckResultDto
            {
                TotalFiles = files.Count
            };

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var fileInfo = new DuplicateFileInfoDto
                {
                    FileName = Path.GetFileName(file.FileName),
                    FileSize = file.Length,
                    IsDuplicate = false
                };

                try
                {
                    // Calculate checksum
                    using var stream = file.OpenReadStream();
                    var checksum = await ComputeFileChecksumAsync(stream);
                    fileInfo.FileChecksum = checksum;

                    // Check for existing asset with same checksum
                    var query = _assetRepo.Query().Where(a => a.FileChecksum == checksum && !a.IsDeleted);
                    
                    if (companyId.HasValue)
                        query = query.Where(a => a.CompanyId == companyId.Value);
                    else
                        query = query.Where(a => a.CompanyId == null);

                    var existingAsset = await query.FirstOrDefaultAsync();

                    if (existingAsset != null)
                    {
                        fileInfo.IsDuplicate = true;
                        fileInfo.ExistingAsset = _mapper.Map<AssetDto>(existingAsset);
                        result.DuplicateCount++;

                        _logger.LogInformation("Duplicate detected: {FileName} matches existing asset {ExistingId} with checksum {Checksum}",
                            fileInfo.FileName, existingAsset.Id, checksum);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking duplicate for file {FileName}", fileInfo.FileName);
                }

                result.Files.Add(fileInfo);
            }

            return result;
        }

        public async Task<DuplicateCheckResultDto> CheckForDuplicatesByChecksumAsync(CheckDuplicatesRequestDto request, string userId, Guid? companyId)
        {
            var result = new DuplicateCheckResultDto
            {
                TotalFiles = request.Files.Count
            };

            foreach (var file in request.Files)
            {
                var fileInfo = new DuplicateFileInfoDto
                {
                    FileName = file.FileName,
                    FileChecksum = file.Checksum,
                    IsDuplicate = false
                };

                try
                {
                    var query = _assetRepo.Query().Where(a => a.FileChecksum == file.Checksum && !a.IsDeleted);

                    if (companyId.HasValue)
                        query = query.Where(a => a.CompanyId == companyId.Value);
                    else
                        query = query.Where(a => a.CompanyId == null);

                    var existingAsset = await query.FirstOrDefaultAsync();

                    if (existingAsset != null)
                    {
                        fileInfo.IsDuplicate = true;
                        fileInfo.ExistingAsset = _mapper.Map<AssetDto>(existingAsset);
                        fileInfo.FileSize = existingAsset.FileSize;
                        result.DuplicateCount++;

                        _logger.LogInformation("Duplicate detected by checksum: {FileName} matches existing asset {ExistingId} with checksum {Checksum}",
                            fileInfo.FileName, existingAsset.Id, file.Checksum);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking duplicate by checksum for file {FileName}", fileInfo.FileName);
                }

                result.Files.Add(fileInfo);
            }

            return result;
        }

        public async Task<UploadWithDuplicateResultDto> UploadAssetsWithDuplicateHandlingAsync(
            IFormFileCollection files,
            List<DuplicateActionDto> actions,
            Guid? folderId,
            string userId,
            Guid? companyId)
        {
            if (files == null || files.Count == 0)
            {
                throw new Exception("No files provided");
            }

            // Verify folder ownership if folderId is provided
            if (folderId.HasValue)
            {
                var folderQuery = _folderRepo.Query().Where(f => f.Id == folderId.Value);
                
                if (companyId.HasValue)
                    folderQuery = folderQuery.Where(f => f.CompanyId == companyId.Value);
                else
                    folderQuery = folderQuery.Where(f => f.CompanyId == null);

                var folder = await folderQuery.FirstOrDefaultAsync();

                if (folder == null)
                {
                    throw new Exception("Folder not found or access denied");
                }
            }

            var result = new UploadWithDuplicateResultDto
            {
                TotalFiles = files.Count
            };

            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "assets", userId);
            var thumbnailsPath = Path.Combine(_environment.WebRootPath, "uploads", "thumbnails", userId);

            Directory.CreateDirectory(uploadsPath);
            Directory.CreateDirectory(thumbnailsPath);

            // Get existing filenames for unique naming
            var existingQuery = _assetRepo.Query().Where(a => a.FolderId == folderId && !a.IsDeleted);
            
            if (companyId.HasValue)
                existingQuery = existingQuery.Where(a => a.CompanyId == companyId.Value);
            else
                existingQuery = existingQuery.Where(a => a.CompanyId == null);

            var existingFileNames = await existingQuery
                .Select(a => a.FileName.ToLower())
                .ToListAsync();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var fileName = Path.GetFileName(file.FileName);
                var action = actions.FirstOrDefault(a => a.FileName == fileName)?.Action ?? DuplicateAction.UploadAnyway;

                var resultItem = new UploadResultItemDto
                {
                    FileName = fileName,
                    Success = false
                };

                try
                {
                    // Calculate checksum
                    string checksum;
                    using (var stream = file.OpenReadStream())
                    {
                        checksum = await ComputeFileChecksumAsync(stream);
                    }

                    // Check for existing asset
                    var existingQuery2 = _assetRepo.Query().Where(a => a.FileChecksum == checksum && !a.IsDeleted);
                    
                    if (companyId.HasValue)
                        existingQuery2 = existingQuery2.Where(a => a.CompanyId == companyId.Value);
                    else
                        existingQuery2 = existingQuery2.Where(a => a.CompanyId == null);

                    var existingAsset = await existingQuery2.FirstOrDefaultAsync();

                    if (existingAsset != null && action == DuplicateAction.Skip)
                    {
                        // Skip upload
                        resultItem.Success = true;
                        resultItem.Status = "skipped";
                        resultItem.Message = "File skipped - duplicate found";
                        resultItem.Asset = _mapper.Map<AssetDto>(existingAsset);
                        result.SkippedCount++;
                    }
                    else if (existingAsset != null && action == DuplicateAction.Replace)
                    {
                        // Replace existing file
                        var oldFilePath = existingAsset.FilePath;
                        var oldThumbnailPath = existingAsset.ThumbnailPath;

                        // Delete old files
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                        if (!string.IsNullOrEmpty(oldThumbnailPath) && File.Exists(oldThumbnailPath))
                        {
                            File.Delete(oldThumbnailPath);
                        }

                        // Save new file with same name
                        var physicalFileName = $"{Guid.NewGuid()}_{existingAsset.FileName}";
                        var filePath = Path.Combine(uploadsPath, physicalFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Update asset
                        var fileType = GetFileType(file.ContentType, existingAsset.FileName);
                        existingAsset.FilePath = filePath;
                        existingAsset.FileSize = file.Length;
                        existingAsset.MimeType = file.ContentType;
                        existingAsset.FileType = fileType;
                        existingAsset.FileChecksum = checksum;
                        existingAsset.UpdatedAt = DateTime.UtcNow;

                        // Regenerate thumbnail
                        if (fileType == "image" || fileType == "video")
                        {
                            existingAsset.ThumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                                filePath,
                                thumbnailsPath,
                                fileType,
                                file.ContentType);
                        }

                        // Re-extract IPTC
                        if (fileType == "image")
                        {
                            existingAsset.IptcMetadata = await _iptcExtractionService.ExtractIptcMetadataAsync(filePath);
                        }

                        _assetRepo.Update(existingAsset);
                        await _assetRepo.SaveAsync();

                        resultItem.Success = true;
                        resultItem.Status = "replaced";
                        resultItem.Message = "File replaced successfully";
                        resultItem.Asset = _mapper.Map<AssetDto>(existingAsset);
                        result.ReplacedCount++;
                    }
                    else
                    {
                        // Upload anyway (new file)
                        var originalFileName = Path.GetFileName(file.FileName);
                        var uniqueFileName = GetUniqueFileName(originalFileName, existingFileNames);
                        existingFileNames.Add(uniqueFileName.ToLower());

                        var physicalFileName = $"{Guid.NewGuid()}_{uniqueFileName}";
                        var filePath = Path.Combine(uploadsPath, physicalFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        var fileType = GetFileType(file.ContentType, uniqueFileName);

                        string? thumbnailPath = null;
                        string? iptcMetadata = null;

                        if (fileType == "image" || fileType == "video")
                        {
                            thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                                filePath,
                                thumbnailsPath,
                                fileType,
                                file.ContentType);
                        }

                        if (fileType == "image")
                        {
                            iptcMetadata = await _iptcExtractionService.ExtractIptcMetadataAsync(filePath);
                        }

                        var asset = new Asset
                        {
                            Id = Guid.NewGuid(),
                            FileName = uniqueFileName,
                            FilePath = filePath,
                            FileType = fileType,
                            MimeType = file.ContentType,
                            FileSize = file.Length,
                            FileChecksum = checksum,
                            ThumbnailPath = thumbnailPath,
                            IptcMetadata = iptcMetadata,
                            FolderId = folderId,
                            UserId = userId,
                            CompanyId = companyId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _assetRepo.AddAsync(asset);
                        await _assetRepo.SaveAsync();

                        resultItem.Success = true;
                        resultItem.Status = "uploaded";
                        resultItem.Message = "File uploaded successfully";
                        resultItem.Asset = _mapper.Map<AssetDto>(asset);
                        result.UploadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    resultItem.Success = false;
                    resultItem.Status = "error";
                    resultItem.Message = $"Error: {ex.Message}";
                    result.ErrorCount++;
                    _logger.LogError(ex, "Error processing file {FileName}", fileName);
                }

                result.Results.Add(resultItem);
            }

            // Update folder asset count
            if (folderId.HasValue && result.UploadedCount > 0)
            {
                var folder = await _folderRepo.Query().FirstOrDefaultAsync(f => f.Id == folderId.Value);
                if (folder != null)
                {
                    folder.AssetCount = await _assetRepo.Query().CountAsync(a => a.FolderId == folderId.Value && !a.IsDeleted);
                    _folderRepo.Update(folder);
                    await _folderRepo.SaveAsync();
                }
            }

            return result;
        }
    }
}
