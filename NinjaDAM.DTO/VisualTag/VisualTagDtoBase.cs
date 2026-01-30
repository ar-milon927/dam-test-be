using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.VisualTag
{
    public abstract class VisualTagDtoBase
    {
        [Required(ErrorMessage = "Tag name is required")]
        [MaxLength(100, ErrorMessage = "Tag name cannot exceed 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Tag color is required")]
        [RegularExpression(@"^#([A-Fa-f0-9]{6})$", ErrorMessage = "Color must be a valid hex color code (e.g., #FF5733)")]
        public string Color { get; set; }
    }
}

