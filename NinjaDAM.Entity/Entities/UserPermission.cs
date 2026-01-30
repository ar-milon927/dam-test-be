using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class UserPermission
    {
        [ForeignKey("User")]
        public string UserId { get; set; }
        public Users User { get; set; }

        public Guid PermissionDetailId { get; set; }
        
        [ForeignKey("PermissionDetailId")]
        public PermissionDetail PermissionDetail { get; set; }
    }
}
