using NinjaDAM.DTO.MetadataField;

namespace NinjaDAM.Services.IServices
{
    public interface IControlledVocabularyValueService
    {
        Task<IEnumerable<ControlledVocabularyValueDto>> GetByFieldIdAsync(Guid fieldId, string userId);
        Task<ControlledVocabularyValueDto> GetByIdAsync(Guid valueId, string userId);
        Task<ControlledVocabularyValueDto> CreateAsync(CreateControlledVocabularyValueDto dto, string userId);
        Task<ControlledVocabularyValueDto> UpdateAsync(Guid valueId, UpdateControlledVocabularyValueDto dto, string userId);
        Task<bool> DeleteAsync(Guid valueId, string userId);
    }
}

