using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    [Table("CartItems")]
    public class CartItem
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public Guid AssetId { get; set; }

        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public Guid? CompanyId { get; set; }
    }
}
