using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class CollectionAsset
    {
        [Key]
        public Guid Id { get; set; }

        // Collection reference
        [Required]
        public Guid CollectionId { get; set; }
        
        [ForeignKey("CollectionId")]
        public Collection Collection { get; set; }

        // Asset reference
        [Required]
        public Guid AssetId { get; set; }
        
        [ForeignKey("AssetId")]
        public Asset Asset { get; set; }

        // When asset was added to collection
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
