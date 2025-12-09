using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnonymizationId",
                table: "PlantData",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CLOEAnalysisId",
                table: "PlantData",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FencillaAnalysisId",
                table: "PlantData",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Anonymization",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IsPersonInImage = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_Anonymization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CLOEAnalysis",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OilLevel = table.Column<float>(type: "real", nullable: true),
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
                    table.PrimaryKey("PK_CLOEAnalysis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FencillaAnalysis",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IsBreak = table.Column<bool>(type: "boolean", nullable: true),
                    Confidence = table.Column<float>(type: "real", nullable: true),
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
                    table.PrimaryKey("PK_FencillaAnalysis", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_AnonymizationId",
                table: "PlantData",
                column: "AnonymizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_CLOEAnalysisId",
                table: "PlantData",
                column: "CLOEAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_FencillaAnalysisId",
                table: "PlantData",
                column: "FencillaAnalysisId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_Anonymization_AnonymizationId",
                table: "PlantData",
                column: "AnonymizationId",
                principalTable: "Anonymization",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_CLOEAnalysis_CLOEAnalysisId",
                table: "PlantData",
                column: "CLOEAnalysisId",
                principalTable: "CLOEAnalysis",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_FencillaAnalysis_FencillaAnalysisId",
                table: "PlantData",
                column: "FencillaAnalysisId",
                principalTable: "FencillaAnalysis",
                principalColumn: "Id");

            // MANUALLY ADDED columns from plant data to new anonymization table
            migrationBuilder.Sql(@"
                WITH inserted_anonymization AS (
                    INSERT INTO public.""Anonymization"" (""Id"", ""SourceBlobStorageLocation_StorageAccount"", ""SourceBlobStorageLocation_BlobContainer"", ""SourceBlobStorageLocation_BlobName"", ""DestinationBlobStorageLocation_StorageAccount"", ""DestinationBlobStorageLocation_BlobContainer"", ""DestinationBlobStorageLocation_BlobName"", ""DateCreated"", ""Status"")
                    SELECT gen_random_uuid(), ""RawDataBlobStorageLocation_StorageAccount"", ""RawDataBlobStorageLocation_BlobContainer"", ""RawDataBlobStorageLocation_BlobName"", ""AnonymizedBlobStorageLocation_StorageAccount"", ""AnonymizedBlobStorageLocation_BlobContainer"", ""AnonymizedBlobStorageLocation_BlobName"", ""DateCreated"", ""AnonymizerWorkflowStatus""
                    FROM public.""PlantData""
                    WHERE ""RawDataBlobStorageLocation_BlobName"" IS NOT NULL
                    RETURNING ""Id"", ""SourceBlobStorageLocation_BlobName""
                )
                UPDATE public.""PlantData""
                SET ""AnonymizationId"" = ia.""Id""
                FROM inserted_anonymization ia
                WHERE public.""PlantData"".""RawDataBlobStorageLocation_BlobName"" = ia.""SourceBlobStorageLocation_BlobName""
                AND public.""PlantData"".""RawDataBlobStorageLocation_BlobName"" IS NOT NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_Anonymization_AnonymizationId",
                table: "PlantData");

            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_CLOEAnalysis_CLOEAnalysisId",
                table: "PlantData");

            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_FencillaAnalysis_FencillaAnalysisId",
                table: "PlantData");

            migrationBuilder.DropTable(
                name: "Anonymization");

            migrationBuilder.DropTable(
                name: "CLOEAnalysis");

            migrationBuilder.DropTable(
                name: "FencillaAnalysis");

            migrationBuilder.DropIndex(
                name: "IX_PlantData_AnonymizationId",
                table: "PlantData");

            migrationBuilder.DropIndex(
                name: "IX_PlantData_CLOEAnalysisId",
                table: "PlantData");

            migrationBuilder.DropIndex(
                name: "IX_PlantData_FencillaAnalysisId",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "AnonymizationId",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "CLOEAnalysisId",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "FencillaAnalysisId",
                table: "PlantData");
        }
    }
}
