using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Permission
{
    public class CreatePermissionDetailDto
    {
        [Required]
        [MaxLength(100)]
        public string PermissionName { get; set; }

        public bool IsActive { get; set; } = true;
        public string? ByDefault { get; set; }
    }
}
