using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.AssetCollection
{
    public class AddAssetToCollectionDto
    {
        [Required(ErrorMessage = "At least one asset ID is required")]
        public List<Guid> AssetIds { get; set; }
    }
}
