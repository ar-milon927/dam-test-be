using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class ControlledVocabularyValue
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid MetadataFieldId { get; set; }

        [ForeignKey("MetadataFieldId")]
        public MetadataField MetadataField { get; set; }

        [Required]
        [MaxLength(200)]
        public string Value { get; set; }

        public int DisplayOrder { get; set; } = 0;

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public Users User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

