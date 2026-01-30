using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.Admin;
using NinjaDAM.Services.IServices;
using System.Security.Claims;

namespace NinjaDAM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "SuperAdmin,Admin")] // Only admins can access
    public class AdminShareLinkController : ControllerBase
    {
        private readonly IAdminShareLinkService _adminShareLinkService;
        private readonly ILogger<AdminShareLinkController> _logger;

        public AdminShareLinkController(
            IAdminShareLinkService adminShareLinkService,
            ILogger<AdminShareLinkController> logger)
        {
            _adminShareLinkService = adminShareLinkService;
            _logger = logger;
        }

        [HttpGet("share-links")]
        public async Task<ActionResult<IEnumerable<AdminShareLinkDto>>> GetAllActiveShareLinks()
        {
            try
            {
                var links = await _adminShareLinkService.GetAllActiveShareLinksAsync();
                return Ok(links);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve share links");
                return BadRequest(new { message = "Failed to retrieve share links" });
            }
        }

        [HttpDelete("share-link/{shareLinkId}")]
        public async Task<ActionResult> DeleteShareLink(Guid shareLinkId, [FromQuery] string shareLinkType)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                if (string.IsNullOrEmpty(shareLinkType) || 
                    (!shareLinkType.Equals("Asset", StringComparison.OrdinalIgnoreCase) && 
                     !shareLinkType.Equals("Collection", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { message = "Invalid share link type. Must be 'Asset' or 'Collection'" });
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var success = await _adminShareLinkService.DeleteShareLinkAsync(shareLinkId, shareLinkType, userId, userName, ipAddress);

                if (!success)
                {
                    return NotFound(new { message = "Share link not found" });
                }

                return Ok(new { message = "Share link deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete share link {LinkId}", shareLinkId);
                return BadRequest(new { message = "Failed to delete share link" });
            }
        }

        [HttpPut("share-link")]
        public async Task<ActionResult> UpdateShareLink([FromBody] UpdateShareLinkDto updateDto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                if (string.IsNullOrEmpty(updateDto.ShareLinkType) || 
                    (!updateDto.ShareLinkType.Equals("Asset", StringComparison.OrdinalIgnoreCase) && 
                     !updateDto.ShareLinkType.Equals("Collection", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { message = "Invalid share link type. Must be 'Asset' or 'Collection'" });
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var success = await _adminShareLinkService.UpdateShareLinkAsync(updateDto, userId, userName, ipAddress);

                if (!success)
                {
                    return NotFound(new { message = "Share link not found or no changes made" });
                }

                return Ok(new { message = "Share link updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update share link {LinkId}", updateDto.ShareLinkId);
                return BadRequest(new { message = "Failed to update share link" });
            }
        }

        [HttpGet("audit-logs")]
        public async Task<ActionResult<IEnumerable<ShareLinkAuditLogDto>>> GetAuditLogs([FromQuery] Guid? shareLinkId, [FromQuery] int limit = 100)
        {
            try
            {
                var logs = await _adminShareLinkService.GetShareLinkAuditLogsAsync(shareLinkId, limit);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs");
                return BadRequest(new { message = "Failed to retrieve audit logs" });
            }
        }
    }
}
