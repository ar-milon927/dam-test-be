namespace NinjaDAM.DTO.AssetShare
{
    public class AssetShareLinkDto
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ShareUrl { get; set; } = string.Empty;
        public bool AllowDownload { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public int? DownloadLimit { get; set; }
        public int DownloadCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TimeRemaining { get; set; } = string.Empty;
    }

    public class CreateAssetShareLinkDto
    {
        public Guid AssetId { get; set; }
        public int? ExpiresInHours { get; set; }
        public DateTime? CustomExpirationDate { get; set; }
        public bool AllowDownload { get; set; }
        public int? DownloadLimit { get; set; }
    }

    public class SharedAssetDetailDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? ThumbnailPath { get; set; }
        public bool AllowDownload { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int? DownloadLimit { get; set; }
        public int DownloadCount { get; set; }
    }
}
