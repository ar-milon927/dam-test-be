using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.Admin;
using NinjaDAM.Entity.Data;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System.Text.Json;

namespace NinjaDAM.Services.Services
{
    public class AdminShareLinkService : IAdminShareLinkService
    {
        private readonly IAssetShareLinkRepository _assetShareLinkRepository;
        private readonly ICollectionShareLinkRepository _collectionShareLinkRepository;
        private readonly IRepository<ShareLinkAuditLog> _auditLogRepository;
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IMapper _mapper;
        private readonly ILogger<AdminShareLinkService> _logger;
        private readonly IConfiguration _configuration;

        public AdminShareLinkService(
            IAssetShareLinkRepository assetShareLinkRepository,
            ICollectionShareLinkRepository collectionShareLinkRepository,
            IRepository<ShareLinkAuditLog> auditLogRepository,
            AppDbContext context,
            UserManager<Users> userManager,
            IMapper mapper,
            ILogger<AdminShareLinkService> logger,
            IConfiguration configuration)
        {
            _assetShareLinkRepository = assetShareLinkRepository;
            _collectionShareLinkRepository = collectionShareLinkRepository;
            _auditLogRepository = auditLogRepository;
            _context = context;
            _userManager = userManager;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IEnumerable<AdminShareLinkDto>> GetAllActiveShareLinksAsync()
        {
            var result = new List<AdminShareLinkDto>();

            // Get all active asset share links
            var assetLinks = await _context.AssetShareLinks
                .Include(x => x.Asset)
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            foreach (var link in assetLinks)
            {
                var user = await _userManager.FindByIdAsync(link.CreatedBy);
                var dto = new AdminShareLinkDto
                {
                    Id = link.Id,
                    ShareLinkType = "Asset",
                    AssetOrCollectionName = link.Asset?.FileName ?? "Unknown Asset",
                    AssetOrCollectionId = link.AssetId,
                    Token = link.Token,
                    ShareUrl = GenerateShareUrl(link.Token, "asset"),
                    AllowDownload = link.AllowDownload,
                    ExpiresAt = link.ExpiresAt,
                    IsActive = link.IsActive,
                    DownloadLimit = link.DownloadLimit,
                    DownloadCount = link.DownloadCount,
                    CreatedBy = link.CreatedBy,
                    CreatedByName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown User",
                    CreatedAt = link.CreatedAt,
                    TimeRemaining = CalculateTimeRemaining(link.ExpiresAt)
                };
                result.Add(dto);
            }

            // Get all active collection share links
            var collectionLinks = await _context.CollectionShareLinks
                .Include(x => x.Collection)
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            foreach (var link in collectionLinks)
            {
                var user = await _userManager.FindByIdAsync(link.CreatedBy);
                var dto = new AdminShareLinkDto
                {
                    Id = link.Id,
                    ShareLinkType = "Collection",
                    AssetOrCollectionName = link.Collection?.Name ?? "Unknown Collection",
                    AssetOrCollectionId = link.CollectionId,
                    Token = link.Token,
                    ShareUrl = GenerateShareUrl(link.Token, "collection"),
                    AllowDownload = link.AllowDownload,
                    ExpiresAt = link.ExpiresAt,
                    IsActive = link.IsActive,
                    DownloadLimit = null,
                    DownloadCount = link.DownloadCount,
                    CreatedBy = link.CreatedBy,
                    CreatedByName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown User",
                    CreatedAt = link.CreatedAt,
                    TimeRemaining = CalculateTimeRemaining(link.ExpiresAt)
                };
                result.Add(dto);
            }

            return result.OrderByDescending(x => x.CreatedAt);
        }

        public async Task<bool> DeleteShareLinkAsync(Guid shareLinkId, string shareLinkType, string adminUserId, string adminUserName, string? ipAddress)
        {
            bool success = false;
            string details = "";

            if (shareLinkType.Equals("Asset", StringComparison.OrdinalIgnoreCase))
            {
                var link = await _assetShareLinkRepository.GetByIdAsync(shareLinkId);
                if (link != null)
                {
                    link.IsActive = false;
                    link.RevokedAt = DateTime.UtcNow;
                    _assetShareLinkRepository.Update(link);
                    await _assetShareLinkRepository.SaveAsync();
                    
                    details = $"Asset: {link.Asset?.FileName}, Token: {link.Token}";
                    success = true;
                }
            }
            else if (shareLinkType.Equals("Collection", StringComparison.OrdinalIgnoreCase))
            {
                var link = await _collectionShareLinkRepository.GetByIdAsync(shareLinkId);
                if (link != null)
                {
                    link.IsActive = false;
                    link.RevokedAt = DateTime.UtcNow;
                    _collectionShareLinkRepository.Update(link);
                    await _collectionShareLinkRepository.SaveAsync();
                    
                    details = $"Collection: {link.Collection?.Name}, Token: {link.Token}";
                    success = true;
                }
            }

            if (success)
            {
                // Log the action
                await LogAuditAsync(shareLinkId, shareLinkType, "Deleted", adminUserId, adminUserName, details, ipAddress);
                _logger.LogInformation("Admin {AdminUserId} deleted {Type} share link {LinkId}", adminUserId, shareLinkType, shareLinkId);
            }

            return success;
        }

        public async Task<bool> UpdateShareLinkAsync(UpdateShareLinkDto updateDto, string adminUserId, string adminUserName, string? ipAddress)
        {
            bool success = false;
            var changes = new List<string>();

            if (updateDto.ShareLinkType.Equals("Asset", StringComparison.OrdinalIgnoreCase))
            {
                var link = await _assetShareLinkRepository.GetByIdAsync(updateDto.ShareLinkId);
                if (link != null)
                {
                    if (updateDto.ExpiresAt.HasValue && updateDto.ExpiresAt.Value != link.ExpiresAt)
                    {
                        changes.Add($"ExpiresAt: {link.ExpiresAt} -> {updateDto.ExpiresAt.Value}");
                        link.ExpiresAt = updateDto.ExpiresAt.Value;
                    }

                    if (updateDto.DownloadLimit.HasValue && updateDto.DownloadLimit.Value != link.DownloadLimit)
                    {
                        changes.Add($"DownloadLimit: {link.DownloadLimit} -> {updateDto.DownloadLimit.Value}");
                        link.DownloadLimit = updateDto.DownloadLimit.Value;
                    }

                    if (changes.Any())
                    {
                        _assetShareLinkRepository.Update(link);
                        await _assetShareLinkRepository.SaveAsync();
                        success = true;
                    }
                }
            }
            else if (updateDto.ShareLinkType.Equals("Collection", StringComparison.OrdinalIgnoreCase))
            {
                var link = await _collectionShareLinkRepository.GetByIdAsync(updateDto.ShareLinkId);
                if (link != null)
                {
                    if (updateDto.ExpiresAt.HasValue && updateDto.ExpiresAt.Value != link.ExpiresAt)
                    {
                        changes.Add($"ExpiresAt: {link.ExpiresAt} -> {updateDto.ExpiresAt.Value}");
                        link.ExpiresAt = updateDto.ExpiresAt.Value;
                    }

                    if (changes.Any())
                    {
                        _collectionShareLinkRepository.Update(link);
                        await _collectionShareLinkRepository.SaveAsync();
                        success = true;
                    }
                }
            }

            if (success && changes.Any())
            {
                var details = string.Join(", ", changes);
                await LogAuditAsync(updateDto.ShareLinkId, updateDto.ShareLinkType, "Updated", adminUserId, adminUserName, details, ipAddress);
                _logger.LogInformation("Admin {AdminUserId} updated {Type} share link {LinkId}: {Details}", 
                    adminUserId, updateDto.ShareLinkType, updateDto.ShareLinkId, details);
            }

            return success;
        }

        public async Task<IEnumerable<ShareLinkAuditLogDto>> GetShareLinkAuditLogsAsync(Guid? shareLinkId = null, int limit = 100)
        {
            IQueryable<ShareLinkAuditLog> query = _context.Set<ShareLinkAuditLog>()
                .OrderByDescending(x => x.PerformedAt);

            if (shareLinkId.HasValue)
            {
                query = query.Where(x => x.ShareLinkId == shareLinkId.Value);
            }

            var logs = await query.Take(limit).ToListAsync();

            return logs.Select(log => new ShareLinkAuditLogDto
            {
                Id = log.Id,
                ShareLinkId = log.ShareLinkId,
                ShareLinkType = log.ShareLinkType,
                Action = log.Action,
                PerformedBy = log.PerformedBy,
                PerformedByName = log.PerformedByName,
                PerformedAt = log.PerformedAt,
                Details = log.Details,
                IpAddress = log.IpAddress
            }).ToList();
        }

        private async Task LogAuditAsync(Guid shareLinkId, string shareLinkType, string action, string userId, string userName, string? details, string? ipAddress)
        {
            var auditLog = new ShareLinkAuditLog
            {
                Id = Guid.NewGuid(),
                ShareLinkId = shareLinkId,
                ShareLinkType = shareLinkType,
                Action = action,
                PerformedBy = userId,
                PerformedByName = userName,
                PerformedAt = DateTime.UtcNow,
                Details = details,
                IpAddress = ipAddress
            };

            await _auditLogRepository.AddAsync(auditLog);
            await _auditLogRepository.SaveAsync();
        }

        private string GenerateShareUrl(string token, string type)
        {
            var frontendUrl = _configuration["FrontendUrl"];
            if (!string.IsNullOrEmpty(frontendUrl))
            {
                return $"{frontendUrl}/shared/{type}/{token}";
            }
            return $"/shared/{type}/{token}";
        }

        private string CalculateTimeRemaining(DateTime expiresAt)
        {
            var timeSpan = expiresAt - DateTime.UtcNow;
            
            if (timeSpan.TotalDays >= 1)
            {
                return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays > 1 ? "s" : "")}";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours > 1 ? "s" : "")}";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes > 1 ? "s" : "")}";
            }
            else
            {
                return "Expired";
            }
        }
    }
}
