using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using NinjaDAM.DTO.ResetPassword;
using System.Security.Claims;

namespace NinjaDAM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResetPasswordController : ControllerBase
    {
        private readonly IResetPasswordService _resetPasswordService;

        public ResetPasswordController(IResetPasswordService resetPasswordService)
        {
            _resetPasswordService = resetPasswordService;
        }

        [Authorize] // User must be logged in  
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ResetPasswordDto dto)
        {
            // Extract email from JWT claims          
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { message = "Email not found in token." });

            var result = await _resetPasswordService.ChangePasswordAsync(dto);
            return Ok(new { message = result });
        }

   
        [HttpPost("change-initial-password")]
        public async Task<IActionResult> ChangeInitialPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _resetPasswordService.ChangeInitialPasswordAsync(dto);

            if (result == "User not found." || result == "Email is required.")
                return NotFound(new { message = result });

            if (result.Contains("incorrect") || result.Contains("required") || result.Contains("long"))
                return BadRequest(new { message = result });

            return Ok(new { message = result });
        }
    }
}
