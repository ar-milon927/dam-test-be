using System;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Asset
{
    public class UpdateAssetDto
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        public Guid? FolderId { get; set; }

        public string? UserMetadata { get; set; }
    }
}
