using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Group
{
    public class CreateGroupDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
