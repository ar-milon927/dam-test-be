using NinjaDAM.DTO.Register;

namespace NinjaDAM.Services.IServices
{
    public interface IRegisterService
    {
        Task<object> RegisterAsync(RegisterDto dto);
    }
}
