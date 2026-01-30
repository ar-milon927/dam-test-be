using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.MetadataField
{
    public class CreateControlledVocabularyValueDto
    {
        [Required(ErrorMessage = "Metadata field ID is required")]
        public Guid MetadataFieldId { get; set; }

        [Required(ErrorMessage = "Value is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Value must be between 1 and 200 characters")]
        public string Value { get; set; }

        public int DisplayOrder { get; set; } = 0;
    }
}

