using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.MetadataField;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class MetadataFieldService : IMetadataFieldService
    {
        private readonly IRepository<MetadataField> _fieldRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<MetadataFieldService> _logger;

        public MetadataFieldService(
            IRepository<MetadataField> fieldRepo,
            IMapper mapper,
            ILogger<MetadataFieldService> logger)
        {
            _fieldRepo = fieldRepo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<MetadataFieldDto>> GetAllAsync(string userId)
        {
            var fields = await _fieldRepo
                .Query()
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.DisplayLabel)
                .ToListAsync();

            return _mapper.Map<IEnumerable<MetadataFieldDto>>(fields);
        }

        public async Task<MetadataFieldDto> GetByIdAsync(Guid fieldId, string userId)
        {
            var field = await _fieldRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == fieldId && f.UserId == userId);

            if (field == null)
            {
                _logger.LogWarning("Metadata field {FieldId} not found for user {UserId}", fieldId, userId);
                return null;
            }

            return _mapper.Map<MetadataFieldDto>(field);
        }

        public async Task<MetadataFieldDto> CreateAsync(CreateMetadataFieldDto dto, string userId, Guid? companyId)
        {
            var existingField = await _fieldRepo
                .Query()
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FieldName.Trim().ToLower() == dto.FieldName.Trim().ToLower());

            if (existingField != null)
            {
                throw new Exception($"A field with the name '{dto.FieldName}' already exists.");
            }

            var field = new MetadataField
            {
                Id = Guid.NewGuid(),
                FieldName = dto.FieldName.Trim(),
                DisplayLabel = dto.DisplayLabel.Trim(),
                Description = dto.Description?.Trim(),
                FieldType = dto.FieldType,
                IsRequired = dto.IsRequired,
                HasControlledVocabulary = dto.HasControlledVocabulary,
                ShowInFilters = dto.ShowInFilters,
                IsMultiSelect = dto.IsMultiSelect,
                UserId = userId,
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _fieldRepo.AddAsync(field);
            await _fieldRepo.SaveAsync();

            _logger.LogInformation("Metadata field {FieldId} created by user {UserId}", field.Id, userId);

            return _mapper.Map<MetadataFieldDto>(field);
        }

        public async Task<MetadataFieldDto> UpdateAsync(Guid fieldId, UpdateMetadataFieldDto dto, string userId)
        {
            var field = await _fieldRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == fieldId && f.UserId == userId);

            if (field == null)
            {
                _logger.LogWarning("Metadata field {FieldId} not found for update by user {UserId}", fieldId, userId);
                return null;
            }

            field.DisplayLabel = dto.DisplayLabel.Trim();
            field.Description = dto.Description?.Trim();
            field.FieldType = dto.FieldType;
            field.IsRequired = dto.IsRequired;
            field.HasControlledVocabulary = dto.HasControlledVocabulary;
            field.ShowInFilters = dto.ShowInFilters;
            field.IsMultiSelect = dto.IsMultiSelect;
            field.UpdatedAt = DateTime.UtcNow;

            _fieldRepo.Update(field);
            await _fieldRepo.SaveAsync();

            _logger.LogInformation("Metadata field {FieldId} updated by user {UserId}", fieldId, userId);

            return _mapper.Map<MetadataFieldDto>(field);
        }

        public async Task<bool> DeleteAsync(Guid fieldId, string userId)
        {
            var field = await _fieldRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == fieldId && f.UserId == userId);

            if (field == null)
            {
                _logger.LogWarning("Metadata field {FieldId} not found for deletion by user {UserId}", fieldId, userId);
                return false;
            }

            _fieldRepo.Delete(field);
            await _fieldRepo.SaveAsync();

            _logger.LogInformation("Metadata field {FieldId} deleted by user {UserId}", fieldId, userId);

            return true;
        }
    }
}
