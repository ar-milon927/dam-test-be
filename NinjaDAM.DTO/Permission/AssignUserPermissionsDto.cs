using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Permission
{
    public class AssignUserPermissionsDto
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public List<Guid> PermissionIds { get; set; } = new List<Guid>();
    }
}
