using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class MetadataField
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; }

        [Required]
        [MaxLength(200)]
        public string DisplayLabel { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string FieldType { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool HasControlledVocabulary { get; set; } = false;

        public bool ShowInFilters { get; set; } = false;

        public bool IsMultiSelect { get; set; } = false;

        [Required]
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        public Users User { get; set; }

        public Guid? CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
