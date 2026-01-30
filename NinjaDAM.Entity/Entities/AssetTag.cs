using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class AssetTag
    {
        [Key]
        public Guid Id { get; set; }

        // Asset reference
        [Required]
        public Guid AssetId { get; set; }
        
        [ForeignKey("AssetId")]
        public Asset Asset { get; set; }

        // VisualTag reference
        [Required]
        public Guid VisualTagId { get; set; }
        
        [ForeignKey("VisualTagId")]
        public VisualTag VisualTag { get; set; }

        // When tag was assigned to asset
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
