using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.DTO.MetadataField;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class ControlledVocabularyValueService : IControlledVocabularyValueService
    {
        private readonly IRepository<ControlledVocabularyValue> _valueRepo;
        private readonly IRepository<MetadataField> _fieldRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<ControlledVocabularyValueService> _logger;

        public ControlledVocabularyValueService(
            IRepository<ControlledVocabularyValue> valueRepo,
            IRepository<MetadataField> fieldRepo,
            IMapper mapper,
            ILogger<ControlledVocabularyValueService> logger)
        {
            _valueRepo = valueRepo;
            _fieldRepo = fieldRepo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ControlledVocabularyValueDto>> GetByFieldIdAsync(Guid fieldId, string userId)
        {
            var field = await _fieldRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == fieldId && f.UserId == userId);

            if (field == null)
            {
                _logger.LogWarning("Metadata field {FieldId} not found for user {UserId}", fieldId, userId);
                return Enumerable.Empty<ControlledVocabularyValueDto>();
            }

            var values = await _valueRepo
                .Query()
                .Where(v => v.MetadataFieldId == fieldId && v.UserId == userId)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.Value)
                .ToListAsync();

            return _mapper.Map<IEnumerable<ControlledVocabularyValueDto>>(values);
        }

        public async Task<ControlledVocabularyValueDto> GetByIdAsync(Guid valueId, string userId)
        {
            var value = await _valueRepo
                .Query()
                .FirstOrDefaultAsync(v => v.Id == valueId && v.UserId == userId);

            if (value == null)
            {
                _logger.LogWarning("Controlled vocabulary value {ValueId} not found for user {UserId}", valueId, userId);
                return null;
            }

            return _mapper.Map<ControlledVocabularyValueDto>(value);
        }

        public async Task<ControlledVocabularyValueDto> CreateAsync(CreateControlledVocabularyValueDto dto, string userId)
        {
            var field = await _fieldRepo
                .Query()
                .FirstOrDefaultAsync(f => f.Id == dto.MetadataFieldId && f.UserId == userId);

            if (field == null)
            {
                throw new Exception("Metadata field not found or access denied.");
            }

            if (!field.HasControlledVocabulary && field.FieldType != "Dropdown")
            {
                throw new Exception("This metadata field does not support controlled vocabulary.");
            }

            var existingValue = await _valueRepo
                .Query()
                .FirstOrDefaultAsync(v => v.MetadataFieldId == dto.MetadataFieldId && 
                                         v.Value.Trim().ToLower() == dto.Value.Trim().ToLower() &&
                                         v.UserId == userId);

            if (existingValue != null)
            {
                throw new Exception($"A value '{dto.Value}' already exists for this field.");
            }

            var value = new ControlledVocabularyValue
            {
                Id = Guid.NewGuid(),
                MetadataFieldId = dto.MetadataFieldId,
                Value = dto.Value.Trim(),
                DisplayOrder = dto.DisplayOrder,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _valueRepo.AddAsync(value);
            await _valueRepo.SaveAsync();

            _logger.LogInformation("Controlled vocabulary value {ValueId} created by user {UserId}", value.Id, userId);

            return _mapper.Map<ControlledVocabularyValueDto>(value);
        }

        public async Task<ControlledVocabularyValueDto> UpdateAsync(Guid valueId, UpdateControlledVocabularyValueDto dto, string userId)
        {
            var value = await _valueRepo
                .Query()
                .FirstOrDefaultAsync(v => v.Id == valueId && v.UserId == userId);

            if (value == null)
            {
                _logger.LogWarning("Controlled vocabulary value {ValueId} not found for update by user {UserId}", valueId, userId);
                return null;
            }

            var existingValue = await _valueRepo
                .Query()
                .FirstOrDefaultAsync(v => v.MetadataFieldId == value.MetadataFieldId && 
                                         v.Id != valueId &&
                                         v.Value.Trim().ToLower() == dto.Value.Trim().ToLower() &&
                                         v.UserId == userId);

            if (existingValue != null)
            {
                throw new Exception($"A value '{dto.Value}' already exists for this field.");
            }

            value.Value = dto.Value.Trim();
            value.DisplayOrder = dto.DisplayOrder;
            value.UpdatedAt = DateTime.UtcNow;

            _valueRepo.Update(value);
            await _valueRepo.SaveAsync();

            _logger.LogInformation("Controlled vocabulary value {ValueId} updated by user {UserId}", valueId, userId);

            return _mapper.Map<ControlledVocabularyValueDto>(value);
        }

        public async Task<bool> DeleteAsync(Guid valueId, string userId)
        {
            var value = await _valueRepo
                .Query()
                .FirstOrDefaultAsync(v => v.Id == valueId && v.UserId == userId);

            if (value == null)
            {
                _logger.LogWarning("Controlled vocabulary value {ValueId} not found for deletion by user {UserId}", valueId, userId);
                return false;
            }

            _valueRepo.Delete(value);
            await _valueRepo.SaveAsync();

            _logger.LogInformation("Controlled vocabulary value {ValueId} deleted by user {UserId}", valueId, userId);

            return true;
        }
    }
}

