using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DTO.Cart;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class CartService : ICartService
    {
        private readonly IRepository<CartItem> _cartRepo;
        private readonly IRepository<Asset> _assetRepo;
        private readonly ILogger<CartService> _logger;

        public CartService(
            IRepository<CartItem> cartRepo,
            IRepository<Asset> assetRepo,
            ILogger<CartService> logger)
        {
            _cartRepo = cartRepo;
            _assetRepo = assetRepo;
            _logger = logger;
        }

        public async Task<CartSummaryDto> GetCartAsync(string userId, Guid? companyId)
        {
            try
            {
                var query = _cartRepo.Query()
                    .Include(ci => ci.Asset)
                    .Where(ci => ci.UserId == userId);

                if (companyId.HasValue)
                    query = query.Where(ci => ci.CompanyId == companyId.Value);
                else
                    query = query.Where(ci => ci.CompanyId == null);

                var cartItems = await query
                    .OrderByDescending(ci => ci.AddedAt)
                    .ToListAsync();

                var items = cartItems
                    .Where(ci => ci.Asset != null && !ci.Asset.IsDeleted)
                    .Select(ci => new CartItemDto
                    {
                        Id = ci.Id,
                        AssetId = ci.AssetId,
                        FileName = ci.Asset!.FileName,
                        FileType = ci.Asset.FileType,
                        FileSize = ci.Asset.FileSize,
                        ThumbnailPath = ci.Asset.ThumbnailPath,
                        FilePath = ci.Asset.FilePath,
                        AddedAt = ci.AddedAt
                    })
                    .ToList();

                return new CartSummaryDto
                {
                    TotalItems = items.Count,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart for user {UserId}", userId);
                throw;
            }
        }

        public async Task<CartItemDto> AddToCartAsync(Guid assetId, string userId, Guid? companyId)
        {
            try
            {
                // Check if asset exists and is accessible
                var assetQuery = _assetRepo.Query().Where(a => a.Id == assetId && !a.IsDeleted);
                
                if (companyId.HasValue)
                    assetQuery = assetQuery.Where(a => a.CompanyId == companyId.Value);
                else
                    assetQuery = assetQuery.Where(a => a.CompanyId == null);

                var asset = await assetQuery.FirstOrDefaultAsync();
                if (asset == null)
                {
                    throw new Exception("Asset not found or access denied");
                }

                // Check if already in cart
                var existingItem = await _cartRepo.Query()
                    .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.AssetId == assetId);

                if (existingItem != null)
                {
                    // Return existing item
                    return new CartItemDto
                    {
                        Id = existingItem.Id,
                        AssetId = existingItem.AssetId,
                        FileName = asset.FileName,
                        FileType = asset.FileType,
                        FileSize = asset.FileSize,
                        ThumbnailPath = asset.ThumbnailPath,
                        FilePath = asset.FilePath,
                        AddedAt = existingItem.AddedAt
                    };
                }

                // Add new cart item
                var cartItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AssetId = assetId,
                    CompanyId = companyId,
                    AddedAt = DateTime.UtcNow
                };

                await _cartRepo.AddAsync(cartItem);
                await _cartRepo.SaveAsync();

                _logger.LogInformation("Added asset {AssetId} to cart for user {UserId}", assetId, userId);

                return new CartItemDto
                {
                    Id = cartItem.Id,
                    AssetId = cartItem.AssetId,
                    FileName = asset.FileName,
                    FileType = asset.FileType,
                    FileSize = asset.FileSize,
                    ThumbnailPath = asset.ThumbnailPath,
                    FilePath = asset.FilePath,
                    AddedAt = cartItem.AddedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding asset {AssetId} to cart for user {UserId}", assetId, userId);
                throw;
            }
        }

        public async Task<List<CartItemDto>> AddMultipleToCartAsync(List<Guid> assetIds, string userId, Guid? companyId)
        {
            var addedItems = new List<CartItemDto>();

            foreach (var assetId in assetIds)
            {
                try
                {
                    var item = await AddToCartAsync(assetId, userId, companyId);
                    addedItems.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add asset {AssetId} to cart for user {UserId}", assetId, userId);
                    // Continue with other assets
                }
            }

            return addedItems;
        }

        public async Task<bool> RemoveFromCartAsync(Guid cartItemId, string userId)
        {
            try
            {
                var cartItem = await _cartRepo.Query()
                    .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

                if (cartItem == null)
                {
                    return false;
                }

                _cartRepo.Delete(cartItem);
                await _cartRepo.SaveAsync();

                _logger.LogInformation("Removed cart item {CartItemId} for user {UserId}", cartItemId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item {CartItemId} for user {UserId}", cartItemId, userId);
                throw;
            }
        }

        public async Task<bool> RemoveMultipleFromCartAsync(List<Guid> cartItemIds, string userId)
        {
            try
            {
                var cartItems = await _cartRepo.Query()
                    .Where(ci => cartItemIds.Contains(ci.Id) && ci.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    return false;
                }

                foreach (var item in cartItems)
                {
                    _cartRepo.Delete(item);
                }

                await _cartRepo.SaveAsync();

                _logger.LogInformation("Removed {Count} cart items for user {UserId}", cartItems.Count, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cart items for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ClearCartAsync(string userId)
        {
            try
            {
                var cartItems = await _cartRepo.Query()
                    .Where(ci => ci.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    return false;
                }

                foreach (var item in cartItems)
                {
                    _cartRepo.Delete(item);
                }

                await _cartRepo.SaveAsync();

                _logger.LogInformation("Cleared cart for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetCartCountAsync(string userId)
        {
            try
            {
                return await _cartRepo.Query()
                    .Include(ci => ci.Asset)
                    .Where(ci => ci.UserId == userId && ci.Asset != null && !ci.Asset.IsDeleted)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count for user {UserId}", userId);
                return 0;
            }
        }
    }
}
