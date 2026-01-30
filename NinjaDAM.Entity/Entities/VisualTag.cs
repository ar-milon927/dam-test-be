using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class VisualTag
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(7)] // Hex color code #RRGGBB
        public string Color { get; set; }

        // User/Company ownership
        [Required]
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        public Users User { get; set; }

        public Guid? CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company? Company { get; set; }

        // Asset count with this tag
        public int AssetCount { get; set; } = 0;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<AssetTag> AssetTags { get; set; } = new List<AssetTag>();
    }
}
