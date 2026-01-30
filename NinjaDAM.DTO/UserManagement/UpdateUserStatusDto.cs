using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.UserManagement
{
    public class UpdateUserStatusDto
    {
        [Required]
        public bool IsActive { get; set; }
    }
}
