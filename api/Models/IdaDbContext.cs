using Microsoft.EntityFrameworkCore;

namespace api.Models
{
    public class IdaDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<InspectionData> InspectionData { get; set; } = null!;

        public DbSet<Analysis> Analysis { get; set; } = null!;
    }
}
