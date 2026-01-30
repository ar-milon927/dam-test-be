using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class ShareLinkAuditLog
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ShareLinkId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ShareLinkType { get; set; } = string.Empty; // "Asset" or "Collection"

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // "Created", "Deleted", "Deactivated", "Updated"

        [Required]
        [MaxLength(450)]
        public string PerformedBy { get; set; } = string.Empty; // User ID

        [MaxLength(100)]
        public string PerformedByName { get; set; } = string.Empty; // User name for quick display

        [Required]
        public DateTime PerformedAt { get; set; }

        [MaxLength(500)]
        public string? Details { get; set; } // JSON or text details of what changed

        [MaxLength(100)]
        public string? IpAddress { get; set; }
    }
}
