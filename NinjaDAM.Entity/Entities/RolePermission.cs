using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace NinjaDAM.Entity.Entities
{
    public class RolePermission
    {
        [Required]
        public string RoleId { get; set; }
        
        [ForeignKey("RoleId")]
        public virtual IdentityRole Role { get; set; }

        [Required]
        public Guid PermissionDetailId { get; set; }

        [ForeignKey("PermissionDetailId")]
        public virtual PermissionDetail PermissionDetail { get; set; }
    }
}
