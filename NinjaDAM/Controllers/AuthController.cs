using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using NinjaDAM.Services.Services;
using NinjaDAM.DTO.login;
using NinjaDAM.DTO.Register;
using NinjaDAM.DTO.ResetPassword;
using NinjaDAM.DTO.ForgotPassword;


namespace NinjaDAM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class AuthController : ControllerBase
    {
        private readonly ILoginService _loginService;
        private readonly IRegisterService _registerService;
        private readonly IForgotPasswordService _forgotPasswordService;
       

        public AuthController(ILoginService loginService, IRegisterService registerService, IForgotPasswordService forgotPasswordService)
        {
            _loginService = loginService;
            _registerService = registerService;
            _forgotPasswordService = forgotPasswordService;
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _loginService.LoginAsync(loginDto);

            if (response == null)
                return Unauthorized(new { message = "Invalid email or password." });

            // Handle first login password reset
            if (response.RequirePasswordReset)
                return Ok(new
                {
                    requirePasswordReset = true,
                    message = response.Message
                });

            // If user is inactive or pending approval 
            if (response.User == null)
                return Ok(new
                {
                    requirePasswordReset = false,
                    message = response.Message
                });

            // Successful login
            return Ok(new
            {
                requirePasswordReset = false,
                message = response.Message,
                user = response.User
            });
        }



        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _registerService.RegisterAsync(registerDto);

            // Extract message from anonymous object  
            var resultMessage = result.GetType().GetProperty("message")?.GetValue(result)?.ToString();
            if (string.IsNullOrWhiteSpace(resultMessage))
                return BadRequest(new { message = "Unknown error occurred." });

            // Email already registered
            if (resultMessage.Contains("already registered", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = resultMessage });

            // Pending approval (all new users are pending)
            if (resultMessage.Contains("pending approval", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    message = resultMessage,
                    email = registerDto.Email,
                    companyName = registerDto.CompanyName,
                    storageTier = (result.GetType().GetProperty("storageTier")?.GetValue(result) ?? "Not selected").ToString()
                });
            }

            // Any other success
            if (resultMessage.Contains("success", StringComparison.OrdinalIgnoreCase))
                return Ok(result);

            // Fallback for unexpected errors
            return BadRequest(result);
        }


        [HttpPost("forgot-password/send-otp")]
        public async Task<IActionResult> SendForgotPasswordOtp([FromBody] ForgotPasswordDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            var result = await _forgotPasswordService.SendForgotPasswordOtpAsync(request);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }


        [HttpPost("forgot-password/verify-otp")]
        public async Task<IActionResult> VerifyForgotPasswordOtp([FromBody] VerifyOtpRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Otp) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Email, OTP, and new password are required." });
            }

            var response = await _forgotPasswordService.VerifyOtpAndResetPasswordAsync(
                request.Email, request.Otp, request.NewPassword);

            return Ok(response);
        }


    }
}
