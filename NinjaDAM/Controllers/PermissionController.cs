using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.Permission;
using NinjaDAM.Services.IServices;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NinjaDAM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize] // Uncomment if auth is required
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpGet("me")]
        public async Task<ActionResult<IEnumerable<PermissionDetailDto>>> GetMyPermissions([FromQuery] string? userId)
        {
            var idToUse = userId;
            
            if (string.IsNullOrEmpty(idToUse))
            {
                idToUse = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? 
                          User.FindFirstValue(ClaimTypes.Name) ??
                          User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            }

            if (string.IsNullOrEmpty(idToUse))
            {
                return BadRequest("User ID not found in request or token.");
            }

            var permissions = await _permissionService.GetPermissionsByUserIdAsync(idToUse);
            return Ok(permissions);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PermissionDetailDto>>> GetAll()
        {
            var permissions = await _permissionService.GetAllPermissionsAsync();
            return Ok(permissions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PermissionDetailDto>> GetById(Guid id)
        {
            try
            {
                var permission = await _permissionService.GetPermissionByIdAsync(id);
                return Ok(permission);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("assign-user")]
        public async Task<IActionResult> AssignPermissionsToUser([FromBody] AssignUserPermissionsDto assignDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _permissionService.AssignPermissionsToUserAsync(assignDto.UserId, assignDto.PermissionIds);
                return Ok(new { message = "Permissions assigned successfully." });
            }
            catch (Exception ex)
            {
                // Should handle specific exceptions like UserNotFound if enforced
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<PermissionDetailDto>>> GetPermissionsByUser(string userId)
        {
            var permissions = await _permissionService.GetPermissionsByUserIdAsync(userId);
            return Ok(permissions);
        }

        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignPermissionsToRole([FromBody] AssignRolePermissionsDto assignDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _permissionService.AssignPermissionsToRoleAsync(assignDto.RoleId, assignDto.PermissionIds);
                return Ok(new { message = "Permissions assigned successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("role/{roleId}")]
        public async Task<ActionResult<IEnumerable<PermissionDetailDto>>> GetPermissionsByRole(string roleId)
        {
            var permissions = await _permissionService.GetPermissionsByRoleIdAsync(roleId);
            return Ok(permissions);
        }

        [HttpPost]
        public async Task<ActionResult<PermissionDetailDto>> Create([FromBody] CreatePermissionDetailDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdPermission = await _permissionService.CreatePermissionAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = createdPermission.Id }, createdPermission);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PermissionDetailDto>> Update(Guid id, [FromBody] UpdatePermissionDetailDto updateDto)
        {
            try
            {
                var updatedPermission = await _permissionService.UpdatePermissionAsync(id, updateDto);
                return Ok(updatedPermission);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _permissionService.DeletePermissionAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
