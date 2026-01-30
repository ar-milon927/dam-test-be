using System;

namespace NinjaDAM.DTO.MetadataField
{
    public class MetadataFieldDto
    {
        public Guid Id { get; set; }
        public string FieldName { get; set; }
        public string DisplayLabel { get; set; }
        public string? Description { get; set; }
        public string FieldType { get; set; }
        public bool IsRequired { get; set; }
        public bool HasControlledVocabulary { get; set; }
        public bool ShowInFilters { get; set; }
        public bool IsMultiSelect { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
