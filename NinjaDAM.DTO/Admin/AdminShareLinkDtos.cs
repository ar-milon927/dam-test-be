namespace NinjaDAM.DTO.Admin
{
    public class AdminShareLinkDto
    {
        public Guid Id { get; set; }
        public string ShareLinkType { get; set; } = string.Empty; // "Asset" or "Collection"
        public string AssetOrCollectionName { get; set; } = string.Empty;
        public Guid AssetOrCollectionId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ShareUrl { get; set; } = string.Empty;
        public bool AllowDownload { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public int? DownloadLimit { get; set; }
        public int DownloadCount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string TimeRemaining { get; set; } = string.Empty;
    }

    public class UpdateShareLinkDto
    {
        public Guid ShareLinkId { get; set; }
        public string ShareLinkType { get; set; } = string.Empty; // "Asset" or "Collection"
        public DateTime? ExpiresAt { get; set; }
        public int? DownloadLimit { get; set; }
    }

    public class ShareLinkAuditLogDto
    {
        public Guid Id { get; set; }
        public Guid ShareLinkId { get; set; }
        public string ShareLinkType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public string PerformedByName { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
    }
}
