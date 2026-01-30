using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Asset
{
    public class BatchMetadataUpdateDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one asset ID is required")]
        public List<Guid> AssetIds { get; set; } = new();

        [Required(ErrorMessage = "Metadata Key is required")]
        public string MetadataKey { get; set; } = string.Empty;
        
        public string MetadataValue { get; set; } = string.Empty;
    }
}
