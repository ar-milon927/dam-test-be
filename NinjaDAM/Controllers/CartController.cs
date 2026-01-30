using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.Cart;
using NinjaDAM.Services.IServices;
using System.Security.Claims;

namespace NinjaDAM.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User ID not found");
        }

        private Guid? GetCompanyId()
        {
            var companyIdClaim = User.FindFirstValue("CompanyId");
            return string.IsNullOrEmpty(companyIdClaim) ? null : Guid.Parse(companyIdClaim);
        }

        /// <summary>
        /// Get all items in the user's cart
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var cart = await _cartService.GetCartAsync(userId, companyId);
                return Ok(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart");
                return StatusCode(500, new { message = "Failed to retrieve cart" });
            }
        }

        /// <summary>
        /// Get the count of items in the user's cart
        /// </summary>
        [HttpGet("count")]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                var userId = GetUserId();
                var count = await _cartService.GetCartCountAsync(userId);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count");
                return StatusCode(500, new { message = "Failed to retrieve cart count" });
            }
        }

        /// <summary>
        /// Add a single asset to cart
        /// </summary>
        [HttpPost("add/{assetId}")]
        public async Task<IActionResult> AddToCart(Guid assetId)
        {
            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var cartItem = await _cartService.AddToCartAsync(assetId, userId, companyId);
                return Ok(new { message = "Added to Asset Cart", item = cartItem });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding asset {AssetId} to cart", assetId);
                return StatusCode(500, new { message = "Failed to add to cart" });
            }
        }

        /// <summary>
        /// Add multiple assets to cart
        /// </summary>
        [HttpPost("add-multiple")]
        public async Task<IActionResult> AddMultipleToCart([FromBody] AddToCartRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var items = await _cartService.AddMultipleToCartAsync(request.AssetIds, userId, companyId);
                return Ok(new { message = $"Added {items.Count} items to Asset Cart", items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding multiple assets to cart");
                return StatusCode(500, new { message = "Failed to add items to cart" });
            }
        }

        /// <summary>
        /// Remove a single item from cart
        /// </summary>
        [HttpDelete("remove/{cartItemId}")]
        public async Task<IActionResult> RemoveFromCart(Guid cartItemId)
        {
            try
            {
                var userId = GetUserId();
                var success = await _cartService.RemoveFromCartAsync(cartItemId, userId);
                
                if (!success)
                {
                    return NotFound(new { message = "Cart item not found" });
                }

                return Ok(new { message = "Removed from cart" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item {CartItemId}", cartItemId);
                return StatusCode(500, new { message = "Failed to remove from cart" });
            }
        }

        /// <summary>
        /// Remove multiple items from cart
        /// </summary>
        [HttpPost("remove-multiple")]
        public async Task<IActionResult> RemoveMultipleFromCart([FromBody] RemoveFromCartRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var success = await _cartService.RemoveMultipleFromCartAsync(request.CartItemIds, userId);
                
                if (!success)
                {
                    return NotFound(new { message = "No cart items found" });
                }

                return Ok(new { message = "Removed items from cart" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cart items");
                return StatusCode(500, new { message = "Failed to remove items from cart" });
            }
        }

        /// <summary>
        /// Clear all items from cart
        /// </summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userId = GetUserId();
                var success = await _cartService.ClearCartAsync(userId);
                
                if (!success)
                {
                    return Ok(new { message = "Cart is already empty" });
                }

                return Ok(new { message = "Cart cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, new { message = "Failed to clear cart" });
            }
        }
    }
}
