using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Database.Context
{
    public class SaraDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PlantData> PlantData { get; set; } = null!;

        public DbSet<Anonymization> Anonymization { get; set; } = null!;

        public DbSet<CLOEAnalysis> CLOEAnalysis { get; set; } = null!;

        public DbSet<FencillaAnalysis> FencillaAnalysis { get; set; } = null!;

        public DbSet<AnalysisMapping> AnalysisMapping { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<AnalysisMapping>()
                .HasIndex(am => new { am.Tag, am.InspectionDescription })
                .IsUnique();
        }
    }
}
