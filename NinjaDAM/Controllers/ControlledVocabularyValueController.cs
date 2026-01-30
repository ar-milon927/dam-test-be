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
    public class ControlledVocabularyValueController : ControllerBase
    {
        private readonly IControlledVocabularyValueService _vocabularyValueService;

        public ControlledVocabularyValueController(IControlledVocabularyValueService vocabularyValueService)
        {
            _vocabularyValueService = vocabularyValueService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet("field/{fieldId}")]
        public async Task<IActionResult> GetByFieldId(Guid fieldId)
        {
            var userId = GetUserId();
            var values = await _vocabularyValueService.GetByFieldIdAsync(fieldId, userId);
            return Ok(values);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = GetUserId();
            var value = await _vocabularyValueService.GetByIdAsync(id, userId);
            
            if (value == null)
            {
                return NotFound(new { message = "Controlled vocabulary value not found" });
            }

            return Ok(value);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateControlledVocabularyValueDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var value = await _vocabularyValueService.CreateAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = value.Id }, value);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateControlledVocabularyValueDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetUserId();
                var value = await _vocabularyValueService.UpdateAsync(id, dto, userId);

                if (value == null)
                {
                    return NotFound(new { message = "Controlled vocabulary value not found" });
                }

                return Ok(new { message = "Controlled vocabulary value updated successfully", value });
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
            var success = await _vocabularyValueService.DeleteAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Controlled vocabulary value not found" });
            }

            return Ok(new { message = "Controlled vocabulary value deleted successfully" });
        }
    }
}

