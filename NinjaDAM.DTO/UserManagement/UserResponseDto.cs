using System;
using NinjaDAM.DTO.Company;

namespace NinjaDAM.DTO.UserManagement
{
    public class UserResponseDto
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public Guid? GroupId { get; set; }
        public string? GroupName { get; set; }
        public CompanyDto? Company { get; set; }
        public bool IsActive { get; set; }
        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
