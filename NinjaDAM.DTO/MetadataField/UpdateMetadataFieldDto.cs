using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.MetadataField
{
    public class UpdateMetadataFieldDto
    {
        [Required(ErrorMessage = "Display label is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Display label must be between 1 and 200 characters")]
        public string DisplayLabel { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Field type is required")]
        public string FieldType { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool HasControlledVocabulary { get; set; } = false;

        public bool ShowInFilters { get; set; } = false;

        public bool IsMultiSelect { get; set; } = false;
    }
}
