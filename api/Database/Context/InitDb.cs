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

            return [record];
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
