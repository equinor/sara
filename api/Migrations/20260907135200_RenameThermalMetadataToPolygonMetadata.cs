using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RenameThermalMetadataToPolygonMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "ThermalReferenceMetadata", newName: "ReferencePolygonMetadata");

            migrationBuilder.RenameIndex(
                name: "IX_ThermalReferenceMetadata_InstallationCode_TagId_InspectionD~",
                table: "ReferencePolygonMetadata",
                newName: "IX_ReferencePolygonMetadata_InstallationCode_TagId_InspectionD~");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                newName: "IX_ThermalReferenceMetadata_InstallationCode_TagId_InspectionD~",
                table: "ReferencePolygonMetadata",
                name: "IX_ReferencePolygonMetadata_InstallationCode_TagId_InspectionD~");

            migrationBuilder.RenameTable(newName: "ThermalReferenceMetadata", name: "ReferencePolygonMetadata");
        }
    }
}
