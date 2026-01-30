using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.AssetCollection
{
    public class UpdateCollectionDto
    {
        [Required(ErrorMessage = "Collection name is required")]
        [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
        public string Name { get; set; }

        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        public Guid? CoverPhotoAssetId { get; set; }
    }
}
