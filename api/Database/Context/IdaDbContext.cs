using Microsoft.EntityFrameworkCore;

namespace api.Database
{
    public class IdaDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<InspectionData> InspectionData { get; set; } = null!;

        public DbSet<Analysis> Analysis { get; set; } = null!;

        public DbSet<DummyDataToTestMigration> DummyDataToTestMigrations { get; set; } = null!;
    }
}
