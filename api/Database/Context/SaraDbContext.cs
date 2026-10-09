using System.Text.Json;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace api.Database.Context
{
    public class SaraDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<InspectionRecord> InspectionRecords { get; set; } = null!;

        public DbSet<Analysis> Analyses { get; set; } = null!;

        public DbSet<AnalysisRun> AnalysisRuns { get; set; } = null!;

        public DbSet<Workflow> Workflows { get; set; } = null!;

        public DbSet<AnalysisGroup> AnalysisGroups { get; set; } = null!;

        public DbSet<ReferencePolygonMetadata> ReferencePolygonMetadata { get; set; } = null!;

        public DbSet<AnalysisRunFeedback> AnalysisRunFeedbacks { get; set; } = null!;

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
                .Entity<ReferencePolygonMetadata>()
                .HasIndex(r => new
                {
                    r.InstallationCode,
                    r.TagId,
                    r.InspectionDescription,
                })
                .IsUnique();

            var polygonPointsComparer = new ValueComparer<List<ImageCoordinate>?>(
                (c1, c2) =>
                    (c1 == null && c2 == null)
                    || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? null : c.ToList()
            );

            modelBuilder
                .Entity<ReferencePolygonMetadata>()
                .Property(p => p.Polygon)
#pragma warning disable CS8603
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v =>
                        JsonSerializer.Deserialize<List<ImageCoordinate>>(
                            v,
                            (JsonSerializerOptions?)null
                        )
                )
                .Metadata.SetValueComparer(polygonPointsComparer);
#pragma warning restore CS8603

            modelBuilder
                .Entity<ReferencePolygonMetadata>()
                .Property(p => p.SourceAnalysisType)
                .HasConversion<string>();

            modelBuilder.Entity<AnalysisRunFeedback>().HasIndex(f => f.AnalysisRunId).IsUnique();
        }
    }
}
