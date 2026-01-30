using System;

namespace NinjaDAM.DTO.Permission
{
    public class PermissionDetailDto
    {
        public Guid Id { get; set; }
        public string PermissionName { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ByDefault { get; set; }
    }
}
