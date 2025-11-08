using Microsoft.EntityFrameworkCore;
using rds.Models;

namespace rds.Data
{
    public class RdsDbContext : DbContext
    {
        public DbSet<Folder> Folders { get; set; }
        public DbSet<MediaFile> MediaFiles { get; set; }

        public RdsDbContext(DbContextOptions<RdsDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Folder>(entity =>
            {
                entity.HasIndex(e => e.Path).IsUnique();
            });
            
            modelBuilder.Entity<MediaFile>(entity =>
            {
                entity.HasIndex(e => e.Path).IsUnique();
            });
        }
    }
}

