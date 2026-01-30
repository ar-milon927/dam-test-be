using System;
using System.Collections.Generic;
using NinjaDAM.DTO.Asset;

namespace NinjaDAM.DTO.AssetCollection
{
    public class CollectionWithAssetsDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int AssetCount { get; set; }
        public Guid? CoverPhotoAssetId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IEnumerable<AssetDto> Assets { get; set; }
    }
}
