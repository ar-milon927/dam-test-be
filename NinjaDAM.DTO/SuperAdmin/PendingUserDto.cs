using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaDAM.DTO.SuperAdmin
{
    public class PendingUserDto
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? CompanyName { get; set; }
        public bool IsApproved { get; set; }
        public bool IsActive { get; set; }
        public bool IsFirstLogin { get; set; }
        public string? Role { get; set; }
    }
}
