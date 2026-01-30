using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.CollectionShare;
using NinjaDAM.Services.IServices;
using System.Security.Claims;

namespace NinjaDAM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CollectionShareController : ControllerBase
    {
        private readonly ICollectionShareService _shareService;

        public CollectionShareController(ICollectionShareService shareService)
        {
            _shareService = shareService;
        }

        [HttpPost("collection/{collectionId}/share")]
        [Authorize]
        public async Task<ActionResult<CollectionShareLinkDto>> CreateShareLink(Guid collectionId, [FromBody] CreateShareLinkDto createDto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                createDto.CollectionId = collectionId; // Ensure consistency
                var shareLink = await _shareService.CreateShareLinkAsync(createDto, userId);

                return Ok(shareLink);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("shared/collection/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<SharedCollectionDto>> GetSharedCollection(string token)
        {
            try
            {
                var sharedCollection = await _shareService.GetSharedCollectionAsync(token);

                if (sharedCollection == null)
                {
                    return NotFound(new { message = "Shared collection not found" });
                }

                return Ok(sharedCollection);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(410, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("share/{shareLinkId}")]
        [Authorize]
        public async Task<ActionResult> RevokeShareLink(Guid shareLinkId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var success = await _shareService.RevokeShareLinkAsync(shareLinkId, userId);

                if (!success)
                {
                    return NotFound(new { message = "Share link not found" });
                }

                return Ok(new { message = "Share link revoked successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("collection/{collectionId}/share-links")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<CollectionShareLinkDto>>> GetActiveShareLinks(Guid collectionId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var shareLinks = await _shareService.GetActiveShareLinksAsync(collectionId, userId);

                return Ok(shareLinks);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("share/{token}/download")]
        [AllowAnonymous]
        public async Task<ActionResult> IncrementDownloadCount(string token)
        {
            try
            {
                await _shareService.IncrementDownloadCountAsync(token);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("share/{token}/asset/{assetId}/download")]
        [AllowAnonymous]
        public async Task<ActionResult> DownloadAssetFromSharedCollection(string token, Guid assetId)
        {
            try
            {
                var result = await _shareService.DownloadAssetFromSharedCollectionAsync(token, assetId);
                
                if (result == null || !result.HasValue)
                {
                    return NotFound(new { message = "Asset not found in shared collection" });
                }

                var (fileStream, contentType, fileName) = result.Value;
                return File(fileStream, contentType, fileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(410, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
