using System.Collections.Generic;

namespace NinjaDAM.DTO.Asset
{
    public class PagedAssetResultDto
    {
        public IEnumerable<AssetDto> Assets { get; set; } = new List<AssetDto>();
        public int Total { get; set; }
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }
}