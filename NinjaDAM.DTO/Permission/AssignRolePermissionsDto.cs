using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Permission
{
    public class AssignRolePermissionsDto
    {
        [Required]
        public string RoleId { get; set; }

        public List<Guid> PermissionIds { get; set; } = new List<Guid>();
    }
}
