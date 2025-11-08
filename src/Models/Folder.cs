using System;
using System.ComponentModel.DataAnnotations;

namespace rds.Models
{
    public enum DisplayNameMode
    {
        Original,
        Custom,
        Blank
    }

    public class Folder
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Path { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string? DisplayName { get; set; }
        
        public DisplayNameMode DisplayNameMode { get; set; } = DisplayNameMode.Original;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

