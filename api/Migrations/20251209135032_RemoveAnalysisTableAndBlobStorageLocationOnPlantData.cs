using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAnalysisTableAndBlobStorageLocationOnPlantData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_Anonymization_AnonymizationId",
                table: "PlantData");

            migrationBuilder.DropTable(
                name: "Analysis");

            migrationBuilder.DropColumn(
                name: "AnalysisToBeRun",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "AnonymizedBlobStorageLocation_BlobContainer",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "AnonymizedBlobStorageLocation_BlobName",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "AnonymizedBlobStorageLocation_StorageAccount",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "AnonymizerWorkflowStatus",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "RawDataBlobStorageLocation_BlobContainer",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "RawDataBlobStorageLocation_BlobName",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "RawDataBlobStorageLocation_StorageAccount",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "VisualizedBlobStorageLocation_BlobContainer",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "VisualizedBlobStorageLocation_BlobName",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "VisualizedBlobStorageLocation_StorageAccount",
                table: "PlantData");

            migrationBuilder.AlterColumn<string>(
                name: "AnonymizationId",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_Anonymization_AnonymizationId",
                table: "PlantData",
                column: "AnonymizationId",
                principalTable: "Anonymization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_Anonymization_AnonymizationId",
                table: "PlantData");

            migrationBuilder.AlterColumn<string>(
                name: "AnonymizationId",
                table: "PlantData",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int[]>(
                name: "AnalysisToBeRun",
                table: "PlantData",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<string>(
                name: "AnonymizedBlobStorageLocation_BlobContainer",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AnonymizedBlobStorageLocation_BlobName",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AnonymizedBlobStorageLocation_StorageAccount",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AnonymizerWorkflowStatus",
                table: "PlantData",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawDataBlobStorageLocation_BlobContainer",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RawDataBlobStorageLocation_BlobName",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RawDataBlobStorageLocation_StorageAccount",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VisualizedBlobStorageLocation_BlobContainer",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VisualizedBlobStorageLocation_BlobName",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VisualizedBlobStorageLocation_StorageAccount",
                table: "PlantData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Analysis",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlantDataId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DestinationPath_BlobContainer = table.Column<string>(type: "text", nullable: true),
                    DestinationPath_BlobName = table.Column<string>(type: "text", nullable: true),
                    DestinationPath_StorageAccount = table.Column<string>(type: "text", nullable: true),
                    SourcePath_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    SourcePath_BlobName = table.Column<string>(type: "text", nullable: false),
                    SourcePath_StorageAccount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Analysis_PlantData_PlantDataId",
                        column: x => x.PlantDataId,
                        principalTable: "PlantData",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analysis_PlantDataId",
                table: "Analysis",
                column: "PlantDataId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_Anonymization_AnonymizationId",
                table: "PlantData",
                column: "AnonymizationId",
                principalTable: "Anonymization",
                principalColumn: "Id");
        }
    }
}
