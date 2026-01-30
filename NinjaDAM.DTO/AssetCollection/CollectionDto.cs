using System;

namespace NinjaDAM.DTO.AssetCollection
{
    public class CollectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int AssetCount { get; set; }
        public Guid? CoverPhotoAssetId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
