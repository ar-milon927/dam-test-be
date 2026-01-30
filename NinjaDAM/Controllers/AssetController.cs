using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.Asset;
using NinjaDAM.Services.IServices;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class AssetController : ControllerBase
    {
        private readonly IAssetService _assetService;
        private readonly IThumbnailService _thumbnailService;
        private readonly IWebHostEnvironment _environment;

        public AssetController(
            IAssetService assetService,
            IThumbnailService thumbnailService,
            IWebHostEnvironment environment)
        {
            _assetService = assetService;
            _thumbnailService = thumbnailService;
            _environment = environment;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        private Guid? GetCompanyId()
        {
            var companyId = User.FindFirstValue("CompanyId");
            return string.IsNullOrEmpty(companyId) ? null : Guid.Parse(companyId);
        }

        [HttpGet]
        public async Task<IActionResult> GetAssets([FromQuery] Guid? folderId, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] int page = 1, [FromQuery] int? pageSize = null)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var result = await _assetService.GetAssetsByFolderAsync(folderId, userId, companyId, sortBy ?? "date", sortDir ?? "desc", page, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAsset(Guid id)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var asset = await _assetService.GetAssetByIdAsync(id, userId, companyId);
            
            if (asset == null)
            {
                return NotFound(new { message = "Asset not found" });
            }

            return Ok(asset);
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAssets([FromForm] IFormFileCollection files, [FromForm] Guid? folderId)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "No files provided" });
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var assets = await _assetService.UploadAssetsAsync(files, folderId, userId, companyId);
                return Ok(new { message = "Files uploaded successfully", assets });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("check-duplicates")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> CheckDuplicates([FromForm] IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "No files provided" });
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var result = await _assetService.CheckForDuplicatesAsync(files, userId, companyId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("check-duplicates-by-checksum")]
        public async Task<IActionResult> CheckDuplicatesByChecksum([FromBody] CheckDuplicatesRequestDto request)
        {
            if (request?.Files == null || request.Files.Count == 0)
            {
                return BadRequest(new { message = "No files provided" });
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var result = await _assetService.CheckForDuplicatesByChecksumAsync(request, userId, companyId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("total-count")]
        public async Task<IActionResult> GetTotalCount()
        {
            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var count = await _assetService.GetTotalAssetCountAsync(userId, companyId);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("upload-with-handling")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadWithDuplicateHandling([FromForm] IFormFileCollection files, [FromForm] string? actionsJson, [FromForm] Guid? folderId)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "No files provided" });
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();

                var actions = new List<DuplicateActionDto>();
                if (!string.IsNullOrEmpty(actionsJson))
                {
                    actions = System.Text.Json.JsonSerializer.Deserialize<List<DuplicateActionDto>>(actionsJson) ?? new List<DuplicateActionDto>();
                }

                var result = await _assetService.UploadAssetsWithDuplicateHandlingAsync(files, actions, folderId, userId, companyId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadAsset(Guid id)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var asset = await _assetService.GetAssetByIdAsync(id, userId, companyId);
            
            if (asset == null)
            {
                return NotFound(new { message = "Asset not found" });
            }

            var fileBytes = await _assetService.DownloadAssetAsync(id, userId, companyId);
            
            if (fileBytes == null)
            {
                return NotFound(new { message = "File not found" });
            }

            return File(fileBytes, asset.MimeType, asset.FileName);
        }

        [HttpPost("download/batch")]
        public async Task<IActionResult> DownloadAssetsBatch([FromBody] DownloadBatchDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var zipBytes = await _assetService.DownloadAssetsAsZipAsync(dto.AssetIds, userId, companyId);
            
            if (zipBytes == null || zipBytes.Length == 0)
            {
                return NotFound(new { message = "No assets found or accessible" });
            }

            var fileName = $"assets_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
            return File(zipBytes, "application/zip", fileName);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsset(Guid id, [FromBody] UpdateAssetDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var updatedAsset = await _assetService.UpdateAssetAsync(id, userId, companyId, dto);

            if (updatedAsset == null)
            {
                return NotFound(new { message = "Asset or folder not found" });
            }

            return Ok(new { message = "Asset updated successfully", asset = updatedAsset });
        }

        [HttpPost("update-metadata-batch")]
        public async Task<IActionResult> UpdateAssetsMetadataBatch([FromBody] BatchMetadataUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var count = await _assetService.UpdateAssetsMetadataAsync(dto.AssetIds, userId, companyId, dto.MetadataKey, dto.MetadataValue);

            if (count == 0)
            {
                return NotFound(new { message = "No assets found" });
            }

            return Ok(new { message = $"Metadata updated for {count} assets" });
        }

        [HttpPost("{id}/move")]
        public async Task<IActionResult> MoveAsset(Guid id, [FromBody] MoveAssetRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.NewFolderId == Guid.Empty)
            {
                return BadRequest(new { message = "NewFolderId is required and cannot be empty" });
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.MoveAssetAsync(id, request.NewFolderId, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "Asset or folder not found" });
            }

            return Ok(new { message = "Asset moved successfully" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsset(Guid id)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.DeleteAssetAsync(id, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "Asset not found" });
            }

            return Ok(new { message = "Asset deleted successfully" });
        }

        [HttpPost("delete-batch")]
        public async Task<IActionResult> DeleteAssetsBatch([FromBody] DeleteBatchDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.DeleteAssetsAsync(dto.AssetIds, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "No assets found" });
            }

            return Ok(new { message = $"{dto.AssetIds.Count} assets deleted successfully" });
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchAssets([FromQuery] Guid? folderId, [FromQuery] string? fileType, [FromQuery] string? keyword, [FromQuery] string? assetIds, [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] int page = 1, [FromQuery] int? pageSize = null)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            
            List<Guid>? parsedAssetIds = null;
            if (!string.IsNullOrEmpty(assetIds))
            {
                parsedAssetIds = assetIds.Split(',')
                    .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g.Value)
                    .ToList();
            }

            var result = await _assetService.SearchAssetsAsync(userId, companyId, folderId, fileType, keyword, sortBy ?? "date", sortDir ?? "desc", page, pageSize, parsedAssetIds);
            return Ok(new { assets = result.Assets, total = result.Total, page = result.Page, has_more = result.HasMore });
        }

        [HttpPost("advanced-search")]
        public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var result = await _assetService.AdvancedSearchAssetsAsync(userId, companyId, request);
            return Ok(new { assets = result.Assets, total = result.Total, page = result.Page, has_more = result.HasMore });
        }

        [HttpGet("recycle-bin")]
        public async Task<IActionResult> GetDeletedAssets([FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] int page = 1, [FromQuery] int? pageSize = null)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var result = await _assetService.GetDeletedAssetsAsync(userId, companyId, sortBy ?? "date", sortDir ?? "desc", page, pageSize);
            return Ok(new { assets = result.Assets, total = result.Total, page = result.Page, has_more = result.HasMore });
        }

        [HttpPost("{id}/restore")]
        public async Task<IActionResult> RestoreAsset(Guid id)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.RestoreAssetAsync(id, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "Asset not found in recycle bin" });
            }

            return Ok(new { message = "Asset restored successfully" });
        }

        [HttpPost("restore-batch")]
        public async Task<IActionResult> RestoreAssetsBatch([FromBody] RestoreBatchDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.RestoreAssetsAsync(dto.AssetIds, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "No assets found in recycle bin" });
            }

            return Ok(new { message = $"{dto.AssetIds.Count} assets restored successfully" });
        }

        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteAsset(Guid id)
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.PermanentlyDeleteAssetAsync(id, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "Asset not found in recycle bin" });
            }

            return Ok(new { message = "Asset permanently deleted" });
        }

        [HttpPost("permanent-delete-batch")]
        public async Task<IActionResult> PermanentlyDeleteAssetsBatch([FromBody] DeleteBatchDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var companyId = GetCompanyId();
            var success = await _assetService.PermanentlyDeleteAssetsAsync(dto.AssetIds, userId, companyId);

            if (!success)
            {
                return NotFound(new { message = "No assets found" });
            }

            return Ok(new { message = $"{dto.AssetIds.Count} assets permanently deleted" });
        }

        [HttpPost("regenerate-thumbnails")]
        public async Task<IActionResult> RegenerateThumbnails()
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var count = await _thumbnailService.RegenerateThumbnailsForUserAsync(userId, companyId, _environment.WebRootPath);
            return Ok(new { message = $"Regenerated {count} thumbnails", count });
        }

        [HttpPost("extract-iptc")]
        public async Task<IActionResult> ExtractIptcMetadata()
        {
            var userId = GetUserId();
            var companyId = GetCompanyId();
            var count = await _assetService.ExtractIptcForExistingAssetsAsync(userId, companyId);
            return Ok(new { message = $"Extracted IPTC metadata for {count} images", count });
        }
    }

    public class DeleteBatchDto
    {
        [Required(ErrorMessage = "AssetIds is required")]
        [MinLength(1, ErrorMessage = "At least one asset ID is required")]
        public List<Guid> AssetIds { get; set; } = new();
    }

    public class RestoreBatchDto
    {
        [Required(ErrorMessage = "AssetIds is required")]
        [MinLength(1, ErrorMessage = "At least one asset ID is required")]
        public List<Guid> AssetIds { get; set; } = new();
    }

    public class MoveAssetRequest
    {
        [Required(ErrorMessage = "NewFolderId is required")]
        public Guid NewFolderId { get; set; }
    }

    public class DownloadBatchDto
    {
        [Required(ErrorMessage = "AssetIds is required")]
        [MinLength(1, ErrorMessage = "At least one asset ID is required")]
        public List<Guid> AssetIds { get; set; } = new();
    }
}
