using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class Asset
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string FileName { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; }

        // File metadata
        [MaxLength(100)]
        public string FileType { get; set; } // image, video, document, etc.

        [MaxLength(50)]
        public string MimeType { get; set; }

        public long FileSize { get; set; } // in bytes

        // File checksum for duplicate detection (SHA-256)
        [MaxLength(64)]
        public string? FileChecksum { get; set; }

        // Thumbnail path for preview
        [MaxLength(500)]
        public string? ThumbnailPath { get; set; }

        // Folder association
        public Guid? FolderId { get; set; }
        
        [ForeignKey("FolderId")]
        public Folder? Folder { get; set; }

        // User/Company ownership
        [Required]
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        public Users User { get; set; }

        public Guid? CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company? Company { get; set; }

        // Metadata tags (JSON string)
        public string? Tags { get; set; }

        // User-defined metadata (JSON string)
        public string? UserMetadata { get; set; }

        // IPTC/EXIF metadata (JSON string)
        public string? IptcMetadata { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ICollection<AssetTag> AssetTags { get; set; } = new List<AssetTag>();
    }
}
