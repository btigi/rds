using System;
using System.ComponentModel.DataAnnotations;

namespace rds.Models
{
    public class Folder
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Path { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

