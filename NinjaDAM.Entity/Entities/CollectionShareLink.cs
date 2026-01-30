using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class CollectionShareLink
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CollectionId { get; set; }

        [ForeignKey(nameof(CollectionId))]
        public virtual Collection? Collection { get; set; }

        [Required]
        [MaxLength(128)]
        public string Token { get; set; } = string.Empty;

        public bool AllowDownload { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int DownloadCount { get; set; } = 0;

        [Required]
        [MaxLength(450)]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? RevokedAt { get; set; }
    }
}
