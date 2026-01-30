using System;

namespace NinjaDAM.Entity.Entities
{
    public class GroupPermission
    {
        public Guid GroupId { get; set; }
        public Group Group { get; set; }

        public Guid PermissionDetailId { get; set; }
        public PermissionDetail PermissionDetail { get; set; }
    }
}
