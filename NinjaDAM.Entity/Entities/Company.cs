using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.Entity.Entities
{
    public class Company
    {
        
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Company name is required.")]
       
        public string CompanyName { get; set; }    

        public string? StorageTier { get; set; } 

        public bool IsActive { get; set; } = true;   
        public bool IsDeleted { get; set; }=false;  

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; } 
        public ICollection<Users> Users { get; set; } = new List<Users>();
    }
}
    