using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using NinjaDAM.DTO.MetadataField;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class MetadataFieldController : ControllerBase
    {
        private readonly IMetadataFieldService _metadataFieldService;

        public MetadataFieldController(IMetadataFieldService metadataFieldService)
        {
            _metadataFieldService = metadataFieldService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        private Guid? GetCompanyId()
        {
            var companyId = User.FindFirstValue("CompanyId");
            return string.IsNullOrEmpty(companyId) ? null : Guid.Parse(companyId);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            var fields = await _metadataFieldService.GetAllAsync(userId);
            return Ok(fields);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = GetUserId();
            var field = await _metadataFieldService.GetByIdAsync(id, userId);
            
            if (field == null)
            {
                return NotFound(new { message = "Metadata field not found" });
            }

            return Ok(field);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMetadataFieldDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var companyId = GetCompanyId();
                var field = await _metadataFieldService.CreateAsync(dto, userId, companyId);
                return CreatedAtAction(nameof(GetById), new { id = field.Id }, field);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMetadataFieldDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var field = await _metadataFieldService.UpdateAsync(id, dto, userId);

                if (field == null)
                {
                    return NotFound(new { message = "Metadata field not found" });
                }

                return Ok(new { message = "Metadata field updated successfully", field });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetUserId();
            var success = await _metadataFieldService.DeleteAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Metadata field not found" });
            }

            return Ok(new { message = "Metadata field deleted successfully" });
        }
    }
}
