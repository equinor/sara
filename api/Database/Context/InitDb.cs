using api.Database.Context;
using api.Database.Models;
using Microsoft.Extensions.Configuration;

namespace Api.Database.Context
{
    public static class InitDb
    {
        private static List<InspectionRecord> GetInspectionRecords()
        {
            var record = new InspectionRecord
            {
                InspectionId = "9df55f01-215e-407e-9778-9a6f3f5dc647",
                InstallationCode = "nls",
                Tag = "tag",
                InspectionDescription = "fencilla",
                BlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = "",
                    BlobContainer = "",
                    BlobName = "",
                },
            };

            var cloeRecord1 = CreateCloeRecord(
                "kaa",
                "tag1",
                "something",
                oilLevel: 0.7f,
                confidence: 0.9f,
                DateTime.UtcNow
            );
            var cloeRecord2 = CreateCloeRecord(
                "kaa",
                "tag1",
                "something",
                oilLevel: 0.3f,
                confidence: 0.8f,
                DateTime.UtcNow.AddDays(-1)
            );
            var cloeRecord3 = CreateCloeRecord(
                "kaa",
                "tag2",
                "something",
                oilLevel: 0.75f,
                confidence: 0.92f,
                DateTime.UtcNow
            );
            var cloeRecord4 = CreateCloeRecord(
                "kaa",
                "tag2",
                "something",
                oilLevel: 0.35f,
                confidence: 0.88f,
                DateTime.UtcNow.AddDays(-1)
            );

            return [record, cloeRecord1, cloeRecord2, cloeRecord3, cloeRecord4];
        }

        private static InspectionRecord CreateCloeRecord(
            string installationCode,
            string tag,
            string inspectionDescription,
            float oilLevel,
            float confidence,
            DateTime createdAt
        )
        {
            var inspectionRecord = new InspectionRecord
            {
                InspectionId = Guid.NewGuid().ToString(),
                InstallationCode = installationCode,
                Tag = tag,
                InspectionDescription = inspectionDescription,
                BlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = "",
                    BlobContainer = "",
                    BlobName = "",
                },
                Analyses = new List<Analysis>(),
                CreatedAt = createdAt,
            };

            var analysis = new Analysis
            {
                Name = "cloe",
                CreatedAt = createdAt,
                Runs = new List<AnalysisRun>(),
            };

            var run = new AnalysisRun
            {
                RunNumber = 1,
                Status = AnalysisRunStatus.Succeeded,
                StartedAt = createdAt,
                CompletedAt = createdAt,
                Analysis = analysis,
                Workflows = new List<Workflow>(),
            };

            var workflow = new Workflow
            {
                StepNumber = 1,
                WorkflowType = "cloe",
                AnalysisRun = run,
                ResultJson =
                    $$"""{"oilLevel": {{oilLevel}}, "confidence": {{confidence}}, "warning": null}""",
                InputBlobStorageLocations = new List<BlobStorageLocation>
                {
                    new BlobStorageLocation
                    {
                        StorageAccount = "",
                        BlobContainer = "",
                        BlobName = "",
                    },
                },
            };

            run.Workflows.Add(workflow);
            analysis.Runs.Add(run);
            inspectionRecord.Analyses.Add(analysis);

            return inspectionRecord;
        }

        private static List<ThermalReferenceMetadata> GetThermalReferenceMetadata(
            IConfiguration configuration
        )
        {
            var storageAccount = configuration["Storage:ThermalReferenceStorageAccount"] ?? "";

            var entry1 = new ThermalReferenceMetadata
            {
                TagId = "thermal",
                InstallationCode = "hua",
                InspectionDescription = "thermal",
                ReferenceImageBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = storageAccount,
                    BlobContainer = "hua",
                    BlobName = "thermal_thermal/reference_image.tiff",
                },
                ReferencePolygonBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = storageAccount,
                    BlobContainer = "hua",
                    BlobName = "thermal_thermal/reference_polygon.json",
                },
            };

            return [entry1];
        }

        public static void PopulateDb(SaraDbContext context, IConfiguration configuration)
        {
            context.AddRange(GetInspectionRecords());
            context.AddRange(GetThermalReferenceMetadata(configuration));

            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
    }
}
