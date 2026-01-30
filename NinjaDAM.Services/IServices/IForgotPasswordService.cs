using NinjaDAM.DTO.ForgotPassword;

namespace NinjaDAM.Services.IServices
{
    public interface IForgotPasswordService
    {
        Task<ForgotPasswordResponseDto> SendForgotPasswordOtpAsync(ForgotPasswordDto request);
        Task<VerifyOtpResponseDto> VerifyOtpAndResetPasswordAsync(string email, string otp, string newPassword);
    }
}
