using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using NinjaDAM.DTO.ForgotPassword;
using System.Threading.Tasks;

namespace NinjaDAM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ForgotPasswordController : ControllerBase
    {
        private readonly IForgotPasswordService _forgotPasswordService;

        public ForgotPasswordController(IForgotPasswordService forgotPasswordService)
        {
            _forgotPasswordService = forgotPasswordService;
        }

      
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] ForgotPasswordDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            var result = await _forgotPasswordService.SendForgotPasswordOtpAsync(request);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
       
   
        [HttpPost("verify-otp")]  
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Otp) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Email, OTP, and new password are required. " });
            }

            var response = await _forgotPasswordService.VerifyOtpAndResetPasswordAsync(
                request.Email, request.Otp, request.NewPassword);
                  
            return Ok(response); 
        }   
    }
}
