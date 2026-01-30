using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.CollectionShare;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System.Security.Cryptography;

namespace NinjaDAM.Services.Services
{
    public class CollectionShareService : ICollectionShareService
    {
        private readonly ICollectionShareLinkRepository _shareLinkRepository;
        private readonly IRepository<Collection> _collectionRepository;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CollectionShareService> _logger;
        private readonly IConfiguration _configuration;

        public CollectionShareService(
            ICollectionShareLinkRepository shareLinkRepository,
            IRepository<Collection> collectionRepository,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CollectionShareService> logger,
            IConfiguration configuration)
        {
            _shareLinkRepository = shareLinkRepository;
            _collectionRepository = collectionRepository;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<CollectionShareLinkDto> CreateShareLinkAsync(CreateShareLinkDto createDto, string userId)
        {
            // Verify collection exists and user has access
            var collection = await _collectionRepository.GetByIdAsync(createDto.CollectionId);
            if (collection == null)
            {
                throw new Exception("Collection not found");
            }

            if (collection.UserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to share this collection");
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

            var shareLink = new CollectionShareLink
            {
                Id = Guid.NewGuid(),
                CollectionId = createDto.CollectionId,
                Token = token,
                AllowDownload = createDto.AllowDownload,
                ExpiresAt = expiresAt,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Creating share link for collection {CollectionId} by user {UserId}", createDto.CollectionId, userId);
            
            await _shareLinkRepository.AddAsync(shareLink);
            await _shareLinkRepository.SaveAsync();
            
            _logger.LogInformation("Share link {ShareLinkId} created successfully with token {Token}", shareLink.Id, token);

            var dto = _mapper.Map<CollectionShareLinkDto>(shareLink);
            dto.ShareUrl = GenerateShareUrl(token);

            return dto;
        }

        public async Task<SharedCollectionDto?> GetSharedCollectionAsync(string token)
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

            var dto = new SharedCollectionDto
            {
                Id = shareLink.Collection.Id,
                Name = shareLink.Collection.Name,
                Description = shareLink.Collection.Description,
                AllowDownload = shareLink.AllowDownload,
                ExpiresAt = shareLink.ExpiresAt,
                Assets = shareLink.Collection.CollectionAssets
                    .Select(ca => _mapper.Map<SharedAssetDto>(ca.Asset))
                    .ToList()
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
            var collection = await _collectionRepository.GetByIdAsync(shareLink.CollectionId);
            if (collection?.UserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to revoke this share link");
            }

            shareLink.IsActive = false;
            shareLink.RevokedAt = DateTime.UtcNow;

            _shareLinkRepository.Update(shareLink);
            await _shareLinkRepository.SaveAsync();
            return true;
        }

        public async Task<IEnumerable<CollectionShareLinkDto>> GetActiveShareLinksAsync(Guid collectionId, string userId)
        {
            // Verify ownership
            var collection = await _collectionRepository.GetByIdAsync(collectionId);
            if (collection == null || collection.UserId != userId)
            {
                throw new UnauthorizedAccessException("You don't have permission to view share links for this collection");
            }

            var shareLinks = await _shareLinkRepository.GetActiveShareLinksAsync(collectionId);

            return shareLinks.Select(sl =>
            {
                var dto = _mapper.Map<CollectionShareLinkDto>(sl);
                dto.ShareUrl = GenerateShareUrl(sl.Token);
                return dto;
            }).ToList();
        }

        public async Task IncrementDownloadCountAsync(string token)
        {
            var shareLink = await _shareLinkRepository.GetByTokenAsync(token);

            if (shareLink != null && shareLink.IsActive && shareLink.ExpiresAt > DateTime.UtcNow)
            {
                shareLink.DownloadCount++;
                _shareLinkRepository.Update(shareLink);
                await _shareLinkRepository.SaveAsync();
            }
        }

        public async Task<int> CleanupExpiredLinksAsync()
        {
            return await _shareLinkRepository.RevokeExpiredLinksAsync();
        }

        public async Task<(Stream FileStream, string ContentType, string FileName)?> DownloadAssetFromSharedCollectionAsync(string token, Guid assetId)
        {
            // Validate the share link
            var shareLink = await _shareLinkRepository.GetByTokenAsync(token);

            if (shareLink == null || !shareLink.IsActive || shareLink.ExpiresAt <= DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("This share link has expired or is invalid.");
            }

            // Check if downloads are allowed
            if (!shareLink.AllowDownload)
            {
                throw new UnauthorizedAccessException("Downloads are disabled for this shared collection.");
            }

            // Verify the asset is in the collection
            var assetInCollection = shareLink.Collection.CollectionAssets
                .Any(ca => ca.AssetId == assetId);

            if (!assetInCollection)
            {
                return null;
            }

            // Get the asset
            var collectionAsset = shareLink.Collection.CollectionAssets
                .FirstOrDefault(ca => ca.AssetId == assetId);

            if (collectionAsset?.Asset == null)
            {
                return null;
            }

            var asset = collectionAsset.Asset;

            // Increment download count
            shareLink.DownloadCount++;
            _shareLinkRepository.Update(shareLink);
            await _shareLinkRepository.SaveAsync();

            // Read file from storage
            if (!File.Exists(asset.FilePath))
            {
                throw new FileNotFoundException("Asset file not found on server.");
            }

            var fileStream = new FileStream(asset.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var contentType = asset.MimeType ?? "application/octet-stream";

            return (fileStream, contentType, asset.FileName);
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
                return $"{frontendUrl}/shared/collection/{token}";
            }
            return $"/shared/collection/{token}";
        }
    }
}
