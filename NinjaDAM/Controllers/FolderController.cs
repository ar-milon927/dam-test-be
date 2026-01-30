using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.Folder;
using NinjaDAM.Services.IServices;
using System.Security.Claims;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class FolderController : ControllerBase
    {
        private readonly IFolderService _folderService;

        public FolderController(IFolderService folderService)
        {
            _folderService = folderService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        private Guid? GetCompanyId()
        {
            var companyId = User.FindFirstValue("CompanyId");
            return string.IsNullOrEmpty(companyId) ? null : Guid.Parse(companyId);
        }

        [HttpGet]
        public async Task<IActionResult> GetFolders()
        {
            var userId = GetUserId();
            var folders = await _folderService.GetUserFoldersAsync(userId);
            return Ok(folders);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFolder(Guid id)
        {
            var userId = GetUserId();
            var folder = await _folderService.GetFolderByIdAsync(id, userId);
            
            if (folder == null)
            {
                return NotFound(new { message = "Folder not found" });
            }

            return Ok(folder);
        }

        [HttpPost]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var folder = await _folderService.CreateFolderAsync(dto, userId, companyId);
                return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, folder);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFolder(Guid id, [FromBody] CreateFolderDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var folder = await _folderService.UpdateFolderAsync(id, dto, userId);
                return Ok(folder);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFolder(Guid id)
        {
            var userId = GetUserId();
            var success = await _folderService.DeleteFolderAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Folder not found" });
            }

            return Ok(new { message = "Folder deleted successfully" });
        }

        [HttpPost("{id}/move")]
        public async Task<IActionResult> MoveFolder(Guid id, [FromBody] MoveFolderDto dto)
        {
            try
            {
                var userId = GetUserId();
                var success = await _folderService.MoveFolderAsync(id, dto.NewParentId, userId);

                if (!success)
                {
                    return NotFound(new { message = "Folder not found" });
                }

                return Ok(new { message = "Folder moved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    // DTO for move operation
    public class MoveFolderDto
    {
        public Guid? NewParentId { get; set; }
    }
}
