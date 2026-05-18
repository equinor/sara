using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisMapping");

            migrationBuilder.DropTable(
                name: "PlantData");

            migrationBuilder.DropTable(
                name: "Anonymization");

            migrationBuilder.DropTable(
                name: "CLOEAnalysis");

            migrationBuilder.DropTable(
                name: "FencillaAnalysis");

            migrationBuilder.DropTable(
                name: "ThermalReading");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisMapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysesToBeRun = table.Column<string>(type: "text", nullable: false),
                    InspectionDescription = table.Column<string>(type: "text", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisMapping", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Anonymization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsPersonInImage = table.Column<bool>(type: "boolean", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DestinationBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    PreProcessedBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: true),
                    PreProcessedBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: true),
                    PreProcessedBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: true),
                    SourceBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anonymization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CLOEAnalysis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OilLevel = table.Column<float>(type: "real", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DestinationBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLOEAnalysis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FencillaAnalysis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsBreak = table.Column<bool>(type: "boolean", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DestinationBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FencillaAnalysis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThermalReading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<float>(type: "real", nullable: true),
                    DestinationBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    DestinationBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    SourceBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermalReading", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlantData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnonymizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CLOEAnalysisId = table.Column<Guid>(type: "uuid", nullable: true),
                    FencillaAnalysisId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThermalReadingAnalysisId = table.Column<Guid>(type: "uuid", nullable: true),
                    Coordinates = table.Column<string>(type: "text", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InspectionDescription = table.Column<string>(type: "text", nullable: true),
                    InspectionId = table.Column<string>(type: "text", nullable: false),
                    InstallationCode = table.Column<string>(type: "text", nullable: false),
                    RobotName = table.Column<string>(type: "text", nullable: true),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantData_Anonymization_AnonymizationId",
                        column: x => x.AnonymizationId,
                        principalTable: "Anonymization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlantData_CLOEAnalysis_CLOEAnalysisId",
                        column: x => x.CLOEAnalysisId,
                        principalTable: "CLOEAnalysis",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlantData_FencillaAnalysis_FencillaAnalysisId",
                        column: x => x.FencillaAnalysisId,
                        principalTable: "FencillaAnalysis",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlantData_ThermalReading_ThermalReadingAnalysisId",
                        column: x => x.ThermalReadingAnalysisId,
                        principalTable: "ThermalReading",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisMapping_Tag_InspectionDescription",
                table: "AnalysisMapping",
                columns: new[] { "Tag", "InspectionDescription" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_AnonymizationId",
                table: "PlantData",
                column: "AnonymizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_CLOEAnalysisId",
                table: "PlantData",
                column: "CLOEAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_DateCreated_Id_Desc",
                table: "PlantData",
                columns: new[] { "DateCreated", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_FencillaAnalysisId",
                table: "PlantData",
                column: "FencillaAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_ThermalReadingAnalysisId",
                table: "PlantData",
                column: "ThermalReadingAnalysisId");
        }
    }
}
