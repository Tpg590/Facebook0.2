using Microsoft.EntityFrameworkCore;

namespace PRN222.Models
{
    public class ClassContext : DbContext
    {
        public DbSet<Student> Students { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server= localhost; Database=StudentDB; Trusted_Connection=True; TrustServerCertificate=True");
        }
    }
}
