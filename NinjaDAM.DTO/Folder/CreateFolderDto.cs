using System;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.Folder
{
    public class CreateFolderDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        public Guid? ParentId { get; set; }

        // Optional: client can send level, else server calculates
        public int? Level { get; set; }
    }
}
