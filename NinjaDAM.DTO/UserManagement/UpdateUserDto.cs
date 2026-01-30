using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.UserManagement
{
    public class UpdateUserDto
    {
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
        public string? FirstName { get; set; }

        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        public string? LastName { get; set; }

        public string? Role { get; set; }

        public Guid? GroupId { get; set; }

        public bool? IsActive { get; set; }
    }
}
