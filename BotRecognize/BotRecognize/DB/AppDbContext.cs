using Microsoft.EntityFrameworkCore;

namespace BotRecognize.DB
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
            Database.EnsureDeleted();  // удаляем бд со старой схемой
            Database.EnsureCreated();
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.UseNpgsql("Host=localhost;Database=epnbot1;Username=postgres;Password=12345Ob@");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

        }
    }
}