using NinjaDAM.DTO.Company;

namespace NinjaDAM.Services.IServices
{
    public interface ICompanyService
    {
        Task<IEnumerable<CompanyDto>> GetAllAsync();
        Task<CompanyDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<CompanyUserResponseDto>> GetUsersByCompanyIdAsync(Guid companyId);
    }
}
