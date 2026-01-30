using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.DTO.Group;
using NinjaDAM.Services.IServices;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class GroupController : ControllerBase
    {
        private readonly IGroupService _groupService;

        public GroupController(IGroupService groupService)
        {
            _groupService = groupService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var groups = await _groupService.GetAllGroupsAsync(GetUserId()!);
            return Ok(groups);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var group = await _groupService.GetGroupByIdAsync(id, GetUserId()!);
                return Ok(group);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGroupDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var group = await _groupService.CreateGroupAsync(dto, GetUserId()!);
                return CreatedAtAction(nameof(GetById), new { id = group.Id }, group);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var group = await _groupService.UpdateGroupAsync(id, dto, GetUserId()!);
                return Ok(group);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _groupService.DeleteGroupAsync(id, GetUserId()!);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpGet("{id}/permissions")]
        public async Task<IActionResult> GetPermissions(Guid id)
        {
            var perms = await _groupService.GetPermissionsByGroupIdAsync(id, GetUserId()!);
            return Ok(perms);
        }

        [HttpPost("{id}/permissions")]
        public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] List<Guid> permissionIds)
        {
            await _groupService.AssignPermissionsToGroupAsync(id, permissionIds, GetUserId()!);
            return Ok(new { message = "Permissions assigned successfully" });
        }
    }
}
