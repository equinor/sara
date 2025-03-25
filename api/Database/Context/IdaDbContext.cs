using Microsoft.EntityFrameworkCore;

namespace api.Database
{
    public class SaraDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PlantData> PlantData { get; set; } = null!;

        public DbSet<Analysis> Analysis { get; set; } = null!;
    }
}
