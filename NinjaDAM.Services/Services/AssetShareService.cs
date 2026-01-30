using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.AssetShare;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System.Security.Cryptography;

namespace NinjaDAM.Services.Services
{
    public class AssetShareService : IAssetShareService
    {
        private readonly IAssetShareLinkRepository _shareLinkRepository;
        private readonly IRepository<Asset> _assetRepository;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AssetShareService> _logger;
        private readonly IConfiguration _configuration;

        public AssetShareService(
            IAssetShareLinkRepository shareLinkRepository,
            IRepository<Asset> assetRepository,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AssetShareService> logger,
            IConfiguration configuration)
        {
            _shareLinkRepository = shareLinkRepository;
            _assetRepository = assetRepository;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AssetShareLinkDto> CreateShareLinkAsync(CreateAssetShareLinkDto createDto, string userId)
        {
            // Verify asset exists and user has access
            var asset = await _assetRepository.GetByIdAsync(createDto.AssetId);
            if (asset == null)
            {
                throw new Exception("Asset not found");
            }

            if (asset.UserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to share this asset");
            }

            // Calculate expiration date with validation
            DateTime expiresAt;
            
            if (createDto.CustomExpirationDate.HasValue)
            {
                // Validate custom expiration date
                var now = DateTime.UtcNow;
                var minExpiration = now.AddDays(1);
                var maxExpiration = now.AddYears(1);
                
                if (createDto.CustomExpirationDate.Value <= now)
                {
                    throw new ArgumentException("Expiration date cannot be in the past");
                }
                
                if (createDto.CustomExpirationDate.Value < minExpiration)
                {
                    throw new ArgumentException("Expiration date must be at least 1 day from now");
                }
                
                if (createDto.CustomExpirationDate.Value > maxExpiration)
                {
                    throw new ArgumentException("Expiration date cannot exceed 1 year from now");
                }
                
                expiresAt = createDto.CustomExpirationDate.Value.ToUniversalTime();
            }
            else
            {
                // Use predefined duration or default to 7 days (168 hours)
                var hoursToAdd = createDto.ExpiresInHours ?? 168;
                
                // Validate hours-based expiration
                if (hoursToAdd < 24)
                {
                    throw new ArgumentException("Expiration must be at least 1 day (24 hours)");
                }
                
                if (hoursToAdd > 8760) // 365 days
                {
                    throw new ArgumentException("Expiration cannot exceed 1 year (8760 hours)");
                }
                
                expiresAt = DateTime.UtcNow.AddHours(hoursToAdd);
            }

            // Generate secure token
            var token = GenerateSecureToken();

            var shareLink = new AssetShareLink
            {
                Id = Guid.NewGuid(),
                AssetId = createDto.AssetId,
                Token = token,
                AllowDownload = createDto.AllowDownload,
                ExpiresAt = expiresAt,
                DownloadLimit = createDto.DownloadLimit,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Creating share link for asset {AssetId} by user {UserId}", createDto.AssetId, userId);
            
            await _shareLinkRepository.AddAsync(shareLink);
            await _shareLinkRepository.SaveAsync();
            
            _logger.LogInformation("Share link {ShareLinkId} created successfully with token {Token}", shareLink.Id, token);

            var dto = _mapper.Map<AssetShareLinkDto>(shareLink);
            dto.ShareUrl = GenerateShareUrl(token);
            dto.TimeRemaining = CalculateTimeRemaining(shareLink.ExpiresAt);

            return dto;
        }

        public async Task<SharedAssetDetailDto?> GetSharedAssetAsync(string token)
        {
            var shareLink = await _shareLinkRepository.GetByTokenAsync(token);

            if (shareLink == null)
            {
                return null;
            }

            // Check if link is expired
            if (!shareLink.IsActive || shareLink.ExpiresAt <= DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("This share link has expired.");
            }

            // Check download limit
            if (shareLink.DownloadLimit.HasValue && shareLink.DownloadCount >= shareLink.DownloadLimit.Value)
            {
                throw new UnauthorizedAccessException("This share link has reached its download limit.");
            }

            var dto = new SharedAssetDetailDto
            {
                Id = shareLink.Asset.Id,
                FileName = shareLink.Asset.FileName,
                FilePath = shareLink.Asset.FilePath,
                FileType = shareLink.Asset.FileType,
                MimeType = shareLink.Asset.MimeType,
                FileSize = shareLink.Asset.FileSize,
                ThumbnailPath = shareLink.Asset.ThumbnailPath,
                AllowDownload = shareLink.AllowDownload,
                ExpiresAt = shareLink.ExpiresAt,
                DownloadLimit = shareLink.DownloadLimit,
                DownloadCount = shareLink.DownloadCount
            };

            return dto;
        }

        public async Task<bool> RevokeShareLinkAsync(Guid shareLinkId, string userId)
        {
            var shareLink = await _shareLinkRepository.GetByIdAsync(shareLinkId);

            if (shareLink == null)
            {
                return false;
            }

            // Verify ownership
            var asset = await _assetRepository.GetByIdAsync(shareLink.AssetId);
            if (asset?.UserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to revoke this share link");
            }

            shareLink.IsActive = false;
            shareLink.RevokedAt = DateTime.UtcNow;

            _shareLinkRepository.Update(shareLink);
            await _shareLinkRepository.SaveAsync();
            return true;
        }

        public async Task<IEnumerable<AssetShareLinkDto>> GetActiveShareLinksAsync(Guid assetId, string userId)
        {
            // Verify ownership
            var asset = await _assetRepository.GetByIdAsync(assetId);
            if (asset == null || asset.UserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to view share links for this asset");
            }

            var shareLinks = await _shareLinkRepository.GetActiveShareLinksAsync(assetId);

            return shareLinks.Select(sl =>
            {
                var dto = _mapper.Map<AssetShareLinkDto>(sl);
                dto.ShareUrl = GenerateShareUrl(sl.Token);
                dto.TimeRemaining = CalculateTimeRemaining(sl.ExpiresAt);
                return dto;
            }).ToList();
        }

        public async Task IncrementDownloadCountAsync(string token)
        {
            var shareLink = await _shareLinkRepository.GetByTokenAsync(token);

            if (shareLink != null && shareLink.IsActive && shareLink.ExpiresAt > DateTime.UtcNow)
            {
                // Check download limit before incrementing
                if (!shareLink.DownloadLimit.HasValue || shareLink.DownloadCount < shareLink.DownloadLimit.Value)
                {
                    shareLink.DownloadCount++;
                    _shareLinkRepository.Update(shareLink);
                    await _shareLinkRepository.SaveAsync();
                }
            }
        }

        public async Task<int> CleanupExpiredLinksAsync()
        {
            return await _shareLinkRepository.RevokeExpiredLinksAsync();
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private string GenerateShareUrl(string token)
        {
            var frontendUrl = _configuration["FrontendUrl"];
            if (!string.IsNullOrEmpty(frontendUrl))
            {
                return $"{frontendUrl}/shared/asset/{token}";
            }
            return $"/shared/asset/{token}";
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
