using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Database.Context
{
    public class IdaDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<InspectionData> InspectionData { get; set; } = null!;

        public DbSet<Analysis> Analysis { get; set; } = null!;
    }
}
