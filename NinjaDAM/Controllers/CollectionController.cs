using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.AssetCollection;
using NinjaDAM.Services.IServices;
using System.Security.Claims;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class CollectionController : ControllerBase
    {
        private readonly ICollectionService _collectionService;

        public CollectionController(ICollectionService collectionService)
        {
            _collectionService = collectionService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        private Guid? GetCompanyId()
        {
            var companyId = User.FindFirstValue("CompanyId");
            return string.IsNullOrEmpty(companyId) ? null : Guid.Parse(companyId);
        }

        /// <summary>
        /// Get all collections for the current user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCollections()
        {
            var userId = GetUserId();
            var collections = await _collectionService.GetUserCollectionsAsync(userId);
            return Ok(collections);
        }

        /// <summary>
        /// Get collection by ID with all assets
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCollection(Guid id)
        {
            var userId = GetUserId();
            var collection = await _collectionService.GetCollectionByIdAsync(id, userId);
            
            if (collection == null)
            {
                return NotFound(new { message = "Collection not found" });
            }

            return Ok(collection);
        }

        /// <summary>
        /// Create new collection
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateCollection([FromBody] CreateCollectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var collection = await _collectionService.CreateCollectionAsync(dto, userId, companyId);
                return CreatedAtAction(nameof(GetCollection), new { id = collection.Id }, collection);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update collection
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCollection(Guid id, [FromBody] UpdateCollectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var updatedCollection = await _collectionService.UpdateCollectionAsync(id, dto, userId);

                if (updatedCollection == null)
                {
                    return NotFound(new { message = "Collection not found" });
                }

                return Ok(new { message = "Collection updated successfully", collection = updatedCollection });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete collection
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCollection(Guid id)
        {
            var userId = GetUserId();
            var success = await _collectionService.DeleteCollectionAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Collection not found" });
            }

            return Ok(new { message = "Collection deleted successfully" });
        }

        /// <summary>
        /// Add assets to collection
        /// </summary>
        [HttpPost("{id}/assets")]
        public async Task<IActionResult> AddAssetsToCollection(Guid id, [FromBody] AddAssetToCollectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var addedCount = await _collectionService.AddAssetsToCollectionAsync(id, dto.AssetIds, userId);

            if (addedCount < 0)
            {
                return NotFound(new { message = "Collection not found or assets don't belong to you" });
            }

            return Ok(new { 
                message = addedCount > 0 
                    ? $"{addedCount} asset{(addedCount != 1 ? "s" : "")} added to collection successfully" 
                    : "Assets already exist in this collection",
                addedCount = addedCount
            });
        }

        /// <summary>
        /// Remove asset from collection
        /// </summary>
        [HttpDelete("{collectionId}/assets/{assetId}")]
        public async Task<IActionResult> RemoveAssetFromCollection(Guid collectionId, Guid assetId)
        {
            var userId = GetUserId();
            var success = await _collectionService.RemoveAssetFromCollectionAsync(collectionId, assetId, userId);

            if (!success)
            {
                return NotFound(new { message = "Collection or asset not found" });
            }

            return Ok(new { message = "Asset removed from collection successfully" });
        }
    }
}
