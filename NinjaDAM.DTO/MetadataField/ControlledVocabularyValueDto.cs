using System;

namespace NinjaDAM.DTO.MetadataField
{
    public class ControlledVocabularyValueDto
    {
        public Guid Id { get; set; }
        public Guid MetadataFieldId { get; set; }
        public string Value { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

