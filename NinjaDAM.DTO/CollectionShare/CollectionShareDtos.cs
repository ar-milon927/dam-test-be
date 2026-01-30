namespace NinjaDAM.DTO.CollectionShare
{
    public class CollectionShareLinkDto
    {
        public Guid Id { get; set; }
        public Guid CollectionId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ShareUrl { get; set; } = string.Empty;
        public bool AllowDownload { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public int DownloadCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateShareLinkDto
    {
        public Guid CollectionId { get; set; }
        public int? ExpiresInHours { get; set; }
        public DateTime? CustomExpirationDate { get; set; }
        public bool AllowDownload { get; set; }
    }

    public class SharedCollectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool AllowDownload { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<SharedAssetDto> Assets { get; set; } = new();
    }

    public class SharedAssetDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public long? FileSize { get; set; }
        public string? ThumbnailPath { get; set; }
        public string? FilePath { get; set; }
    }
}
