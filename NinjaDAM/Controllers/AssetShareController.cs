using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.AssetShare;
using NinjaDAM.Services.IServices;
using System.Security.Claims;

namespace NinjaDAM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AssetShareController : ControllerBase
    {
        private readonly IAssetShareService _shareService;

        public AssetShareController(IAssetShareService shareService)
        {
            _shareService = shareService;
        }

        [HttpPost("asset/{assetId}/share")]
        [Authorize]
        public async Task<ActionResult<AssetShareLinkDto>> CreateShareLink(Guid assetId, [FromBody] CreateAssetShareLinkDto createDto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                createDto.AssetId = assetId; // Ensure consistency
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

        [HttpGet("shared/asset/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetSharedAsset(string token)
        {
            try
            {
                var sharedAsset = await _shareService.GetSharedAssetAsync(token);

                if (sharedAsset == null)
                {
                    return NotFound(new { message = "Shared asset not found" });
                }

                // Return in the format expected by frontend
                return Ok(new
                {
                    asset = sharedAsset,
                    shareInfo = new
                    {
                        allowDownload = sharedAsset.AllowDownload,
                        expiresAt = sharedAsset.ExpiresAt,
                        downloadLimit = sharedAsset.DownloadLimit,
                        downloadCount = sharedAsset.DownloadCount
                    }
                });
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

        [HttpGet("asset/{assetId}/share-links")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<AssetShareLinkDto>>> GetActiveShareLinks(Guid assetId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var shareLinks = await _shareService.GetActiveShareLinksAsync(assetId, userId);

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
    }
}
