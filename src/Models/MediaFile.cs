using System;
using System.ComponentModel.DataAnnotations;

namespace rds.Models
{
    public class MediaFile
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(1000)]
        public string Path { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        public string Extension { get; set; } = string.Empty;
        
        public DateTime ScannedAt { get; set; } = DateTime.Now;
    }
}

