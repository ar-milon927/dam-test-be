using System;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.Entity.Entities
{
    public class Group
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid CompanyId { get; set; }
        public Company Company { get; set; }
    }
}
