using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NinjaDAM.DTO.Asset
{
    public enum DuplicateAction
    {
        Skip,
        UploadAnyway,
        Replace
    }

    public class DuplicateFileInfoDto
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileChecksum { get; set; }
        public bool IsDuplicate { get; set; }
        public AssetDto? ExistingAsset { get; set; }
    }

    public class DuplicateCheckResultDto
    {
        public List<DuplicateFileInfoDto> Files { get; set; } = new List<DuplicateFileInfoDto>();
        public int TotalFiles { get; set; }
        public int DuplicateCount { get; set; }
    }

    public class DuplicateActionDto
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }
        
        [JsonPropertyName("action")]
        public DuplicateAction Action { get; set; }
    }

    public class UploadResultItemDto
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public string Status { get; set; } // "uploaded", "skipped", "replaced", "error"
        public string? Message { get; set; }
        public AssetDto? Asset { get; set; }
    }

    public class UploadWithDuplicateResultDto
    {
        public List<UploadResultItemDto> Results { get; set; } = new List<UploadResultItemDto>();
        public int TotalFiles { get; set; }
        public int UploadedCount { get; set; }
        public int SkippedCount { get; set; }
        public int ReplacedCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public class CheckDuplicatesRequestDto
    {
        public List<FileChecksumDto> Files { get; set; } = new();
    }

    public class FileChecksumDto
    {
        public string FileName { get; set; }
        public string Checksum { get; set; }
    }
}
