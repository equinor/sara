using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddThermalReadingAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThermalReadingAnalysisId",
                table: "PlantData",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ThermalReading",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Temperature = table.Column<float>(type: "real", nullable: true),
                    SourceBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermalReading", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_ThermalReadingAnalysisId",
                table: "PlantData",
                column: "ThermalReadingAnalysisId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_ThermalReading_ThermalReadingAnalysisId",
                table: "PlantData",
                column: "ThermalReadingAnalysisId",
                principalTable: "ThermalReading",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_ThermalReading_ThermalReadingAnalysisId",
                table: "PlantData");

            migrationBuilder.DropTable(
                name: "ThermalReading");

            migrationBuilder.DropIndex(
                name: "IX_PlantData_ThermalReadingAnalysisId",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "ThermalReadingAnalysisId",
                table: "PlantData");
        }
    }
}
