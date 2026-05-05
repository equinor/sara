using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddThermalReferenceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThermalReferenceMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<string>(type: "text", nullable: false),
                    InstallationCode = table.Column<string>(type: "text", nullable: false),
                    InspectionDescription = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceImageBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    ReferenceImageBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    ReferenceImageBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    ReferencePolygonBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    ReferencePolygonBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    ReferencePolygonBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermalReferenceMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThermalReferenceMetadata_InstallationCode_TagId_InspectionD~",
                table: "ThermalReferenceMetadata",
                columns: new[] { "InstallationCode", "TagId", "InspectionDescription" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThermalReferenceMetadata");
        }
    }
}
