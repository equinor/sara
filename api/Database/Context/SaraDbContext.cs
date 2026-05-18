using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Database.Context
{
    public class SaraDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<InspectionRecord> InspectionRecords { get; set; } = null!;

        public DbSet<Analysis> Analyses { get; set; } = null!;

        public DbSet<AnalysisRun> AnalysisRuns { get; set; } = null!;

        public DbSet<Workflow> Workflows { get; set; } = null!;

        public DbSet<AnalysisGroup> AnalysisGroups { get; set; } = null!;

        public DbSet<ThermalReferenceMetadata> ThermalReferenceMetadata { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InspectionRecord>().HasIndex(ir => ir.InspectionId).IsUnique();

            modelBuilder
                .Entity<InspectionRecord>()
                .HasIndex(ir => new { ir.CreatedAt, ir.Id })
                .IsDescending(true, true)
                .HasDatabaseName("IX_InspectionRecord_CreatedAt_Id_Desc");

            modelBuilder
                .Entity<InspectionRecord>()
                .HasMany(ir => ir.Analyses)
                .WithMany(a => a.InspectionRecords);

            modelBuilder.Entity<AnalysisGroup>().HasIndex(ag => ag.GroupId).IsUnique();

            modelBuilder
                .Entity<InspectionRecord>()
                .OwnsOne(
                    ir => ir.RobotPose,
                    pose =>
                    {
                        // Shadow discriminator so EF can distinguish a missing Pose
                        // from a Pose with all-default nested owned values.
                        pose.Property<bool>("HasValue").IsRequired();
                        pose.OwnsOne(p => p.Position);
                        pose.OwnsOne(p => p.Orientation);
                        pose.Navigation(p => p.Position).IsRequired();
                        pose.Navigation(p => p.Orientation).IsRequired();
                    }
                );

            modelBuilder.Entity<InspectionRecord>().OwnsOne(ir => ir.TargetPosition);

            modelBuilder
                .Entity<Workflow>()
                .OwnsMany(
                    w => w.InputBlobStorageLocations,
                    b =>
                    {
                        b.WithOwner().HasForeignKey("WorkflowId");
                        b.Property<int>("Id");
                        b.HasKey("Id");
                    }
                );

            modelBuilder
                .Entity<ThermalReferenceMetadata>()
                .HasIndex(tri => new
                {
                    tri.InstallationCode,
                    tri.TagId,
                    tri.InspectionDescription,
                })
                .IsUnique();
        }
    }
}
