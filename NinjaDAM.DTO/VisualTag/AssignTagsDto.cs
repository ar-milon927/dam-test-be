using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.VisualTag
{
    public class AssignTagsDto
    {
        [Required(ErrorMessage = "Asset IDs are required")]
        public List<Guid> AssetIds { get; set; }

        [Required(ErrorMessage = "Tag IDs are required")]
        public List<Guid> TagIds { get; set; }
    }

    public class RemoveTagsDto
    {
        [Required(ErrorMessage = "Asset IDs are required")]
        public List<Guid> AssetIds { get; set; }

        [Required(ErrorMessage = "Tag IDs are required")]
        public List<Guid> TagIds { get; set; }
    }
}
