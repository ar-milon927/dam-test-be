using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using System;
using System.Threading.Tasks;

namespace NinjaDAM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer", Roles = "SuperAdmin")]
    public class SuperAdminController : ControllerBase
    {
        private readonly ISuperAdminService _superAdminService;

        public SuperAdminController(ISuperAdminService superAdminService)
        {
            _superAdminService = superAdminService;
        }

        [HttpGet("pending-users")]
        public async Task<IActionResult> GetPendingUsers()
        {
            var users = await _superAdminService.GetPendingUsersAsync();
            return Ok(users);
        }

        [HttpPost("approve/{userId}")]
        public async Task<IActionResult> ApproveUser(Guid userId)
        {
            var result = await _superAdminService.ApproveUserAsync(userId);
            return Ok(new { message = result });
        }

        [HttpPost("reject/{userId}")]
        public async Task<IActionResult> RejectUser(Guid userId)
        {
            var result = await _superAdminService.RejectUserAsync(userId);
            return Ok(new { message = result });
        }
    }
}
