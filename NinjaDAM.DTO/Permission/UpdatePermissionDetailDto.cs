using System;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Permission
{
    public class UpdatePermissionDetailDto
    {
        [MaxLength(100)]
        public string? PermissionName { get; set; }

        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public string? ByDefault { get; set; }
    }
}
