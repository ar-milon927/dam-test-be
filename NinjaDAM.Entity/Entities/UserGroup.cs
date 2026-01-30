using System;

namespace NinjaDAM.Entity.Entities
{
    public class UserGroup
    {
        public string UserId { get; set; }
        public Users User { get; set; }

        public Guid GroupId { get; set; }
        public Group Group { get; set; }
    }
}
