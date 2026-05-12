using api.Database.Context;
using api.Database.Models;
using Microsoft.Extensions.Configuration;

namespace Api.Database.Context
{
    public static class InitDb
    {
        private static readonly List<AnalysisMapping> analysisMappings = GetAnalysisMappings();
        private static readonly List<PlantData> plantData = GetPlantData();

        private static List<AnalysisMapping> GetAnalysisMappings()
        {
            var mapping1 = new AnalysisMapping("tag", "fencilla", [AnalysisType.Fencilla]);

            var mapping2 = new AnalysisMapping("tag", "cloe", [AnalysisType.ConstantLevelOiler]);

            var mapping3 = new AnalysisMapping("thermal", "thermal", [AnalysisType.ThermalReading]);

            return new List<AnalysisMapping>([mapping1, mapping2, mapping3]);
        }

        private static List<PlantData> GetPlantData()
        {
            var data1 = new PlantData
            {
                InspectionId = "9df55f01-215e-407e-9778-9a6f3f5dc647",
                InstallationCode = "nls",
                Tag = "tag",
                InspectionDescription = "fencilla",
                Anonymization = new Anonymization
                {
                    SourceBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "",
                        BlobContainer = "",
                        BlobName = "",
                    },
                    DestinationBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "",
                        BlobContainer = "",
                        BlobName = "",
                    },
                },
                FencillaAnalysis = new FencillaAnalysis
                {
                    SourceBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "",
                        BlobContainer = "",
                        BlobName = "",
                    },
                    DestinationBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "",
                        BlobContainer = "",
                        BlobName = "",
                    },
                    IsBreak = true,
                    Confidence = 90,
                },
            };

            return new List<PlantData>([data1]);
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

            return new List<ThermalReferenceMetadata>([entry1]);
        }

        public static void PopulateDb(SaraDbContext context, IConfiguration configuration)
        {
            context.AddRange(analysisMappings);
            context.AddRange(plantData);
            context.AddRange(GetThermalReferenceMetadata(configuration));

            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
    }
}
