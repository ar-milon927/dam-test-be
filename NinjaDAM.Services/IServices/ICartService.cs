using NinjaDAM.DTO.Cart;

namespace NinjaDAM.Services.IServices
{
    public interface ICartService
    {
        Task<CartSummaryDto> GetCartAsync(string userId, Guid? companyId);
        Task<CartItemDto> AddToCartAsync(Guid assetId, string userId, Guid? companyId);
        Task<List<CartItemDto>> AddMultipleToCartAsync(List<Guid> assetIds, string userId, Guid? companyId);
        Task<bool> RemoveFromCartAsync(Guid cartItemId, string userId);
        Task<bool> RemoveMultipleFromCartAsync(List<Guid> cartItemIds, string userId);
        Task<bool> ClearCartAsync(string userId);
        Task<int> GetCartCountAsync(string userId);
    }
}
