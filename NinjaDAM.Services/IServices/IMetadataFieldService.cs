using NinjaDAM.DTO.MetadataField;

namespace NinjaDAM.Services.IServices
{
    public interface IMetadataFieldService
    {
        Task<IEnumerable<MetadataFieldDto>> GetAllAsync(string userId);
        Task<MetadataFieldDto> GetByIdAsync(Guid fieldId, string userId);
        Task<MetadataFieldDto> CreateAsync(CreateMetadataFieldDto dto, string userId, Guid? companyId);
        Task<MetadataFieldDto> UpdateAsync(Guid fieldId, UpdateMetadataFieldDto dto, string userId);
        Task<bool> DeleteAsync(Guid fieldId, string userId);
    }
}
