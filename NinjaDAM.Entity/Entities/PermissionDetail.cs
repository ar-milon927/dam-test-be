using System;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.Entity.Entities
{
    public class PermissionDetail
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionName { get; set; }

        public bool IsDeleted { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? ByDefault { get; set; }

        // Navigation property for potential many-to-many relationship
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }
}
