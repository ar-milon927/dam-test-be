using NinjaDAM.DTO.ResetPassword;

namespace NinjaDAM.Services.IServices
{
    public interface IResetPasswordService
    {
        Task<string> ChangePasswordAsync(ResetPasswordDto dto);
        Task<string> ChangeInitialPasswordAsync(ResetPasswordDto dto);
    }
}
