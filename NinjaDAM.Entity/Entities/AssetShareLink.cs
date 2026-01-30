using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class AssetShareLink
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid AssetId { get; set; }

        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        [Required]
        [MaxLength(128)]
        public string Token { get; set; } = string.Empty;

        public bool AllowDownload { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int? DownloadLimit { get; set; }

        public int DownloadCount { get; set; } = 0;

        [Required]
        [MaxLength(450)]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? RevokedAt { get; set; }
    }
}
