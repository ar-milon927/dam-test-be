using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NinjaDAM.Entity.Entities
{
    public class Folder
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        // Parent folder for nested structure
        public Guid? ParentId { get; set; }
        
        [ForeignKey("ParentId")]
        public Folder? ParentFolder { get; set; }

        // User/Company ownership
        [Required]
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        public Users User { get; set; }

        public Guid? CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company? Company { get; set; }

        // Hierarchy level (0 for root folders)
        public int Level { get; set; } = 0;

        // Asset count in this folder
        public int AssetCount { get; set; } = 0;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Folder> SubFolders { get; set; } = new List<Folder>();
        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}
