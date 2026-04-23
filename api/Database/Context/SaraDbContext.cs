using api.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Database.Context
{
    public class SaraDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PlantData> PlantData { get; set; } = null!;

        public DbSet<Anonymization> Anonymization { get; set; } = null!;

        public DbSet<CLOEAnalysis> CLOEAnalysis { get; set; } = null!;

        public DbSet<FencillaAnalysis> FencillaAnalysis { get; set; } = null!;

        public DbSet<AnalysisMapping> AnalysisMapping { get; set; } = null!;

        public DbSet<ThermalReadingAnalysis> ThermalReading { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            AddConverterForListOfEnums(
                modelBuilder.Entity<AnalysisMapping>().Property(r => r.AnalysesToBeRun)
            );

            modelBuilder
                .Entity<AnalysisMapping>()
                .HasIndex(am => new { am.Tag, am.InspectionDescription })
                .IsUnique();

            modelBuilder
                .Entity<PlantData>()
                .HasIndex(p => new { p.DateCreated, p.Id })
                .IsDescending(true, true)
                .HasDatabaseName("IX_PlantData_DateCreated_Id_Desc");
        }

        private static void AddConverterForListOfEnums<T>(PropertyBuilder<List<T>> propertyBuilder)
            where T : Enum
        {
            propertyBuilder.HasConversion(
                r => r != null ? string.Join(';', r) : "",
                r =>
                    r.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => (T)Enum.Parse(typeof(T), r))
                        .ToList()
            );
        }
    }
}
