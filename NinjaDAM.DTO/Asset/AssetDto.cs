using System;

namespace NinjaDAM.DTO.Asset
{
    public class AssetDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public string MimeType { get; set; }
        public long FileSize { get; set; }
        public string? FileChecksum { get; set; }
        public string? ThumbnailPath { get; set; }
        public Guid? FolderId { get; set; }
        public string? Tags { get; set; }
        public string? UserMetadata { get; set; }
        public string? IptcMetadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
