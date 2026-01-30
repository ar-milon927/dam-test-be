using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using NinjaDAM.DTO.VisualTag;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class VisualTagController : ControllerBase
    {
        private readonly IVisualTagService _visualTagService;

        public VisualTagController(IVisualTagService visualTagService)
        {
            _visualTagService = visualTagService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        private Guid? GetCompanyId()
        {
            var companyId = User.FindFirstValue("CompanyId");
            return string.IsNullOrEmpty(companyId) ? null : Guid.Parse(companyId);
        }

        /// <summary>
        /// Get all visual tags for the current user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            var tags = await _visualTagService.GetAllAsync(userId);
            return Ok(tags);
        }

        /// <summary>
        /// Get visual tag by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = GetUserId();
            var tag = await _visualTagService.GetByIdAsync(id, userId);
            
            if (tag == null)
            {
                return NotFound(new { message = "Visual tag not found" });
            }

            return Ok(tag);
        }

        /// <summary>
        /// Create new visual tag
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateVisualTagDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var tag = await _visualTagService.CreateAsync(dto, userId, companyId);
                return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update visual tag
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVisualTagDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var tag = await _visualTagService.UpdateAsync(id, dto, userId);

                if (tag == null)
                {
                    return NotFound(new { message = "Visual tag not found" });
                }

                return Ok(new { message = "Visual tag updated successfully", tag });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete visual tag
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetUserId();
            var success = await _visualTagService.DeleteAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Visual tag not found" });
            }

            return Ok(new { message = "Visual tag deleted successfully" });
        }

        /// <summary>
        /// Assign tags to assets
        /// </summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignTags([FromBody] AssignTagsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var addedCount = await _visualTagService.AssignTagsToAssetsAsync(dto, userId);
                return Ok(new { message = $"{addedCount} tag assignment{(addedCount != 1 ? "s" : "")} created successfully", addedCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Remove tags from assets
        /// </summary>
        [HttpPost("remove")]
        public async Task<IActionResult> RemoveTags([FromBody] RemoveTagsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var removedCount = await _visualTagService.RemoveTagsFromAssetsAsync(dto, userId);

                if (removedCount == 0)
                {
                    return NotFound(new { message = "No matching tag assignments found" });
                }

                return Ok(new { message = $"{removedCount} tag assignment{(removedCount != 1 ? "s" : "")} removed successfully", removedCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get tags for a specific asset
        /// </summary>
        [HttpGet("asset/{assetId}")]
        public async Task<IActionResult> GetTagsByAsset(Guid assetId)
        {
            var userId = GetUserId();
            var tags = await _visualTagService.GetTagsByAssetIdAsync(assetId, userId);
            return Ok(tags);
        }

        /// <summary>
        /// Get assets with a specific tag
        /// </summary>
        [HttpGet("{tagId}/assets")]
        public async Task<IActionResult> GetAssetsByTag(Guid tagId)
        {
            var userId = GetUserId();
            var assetIds = await _visualTagService.GetAssetIdsByTagIdAsync(tagId, userId);
            return Ok(new { assetIds });
        }
    }
}
