using NinjaDAM.DTO.login;

namespace NinjaDAM.Services.IServices
{
    public interface ILoginService
    {
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto);
       
    }
}
