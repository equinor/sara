using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionData",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InspectionId = table.Column<string>(type: "text", nullable: false),
                    RawDataBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    RawDataBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    RawDataBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    AnonymizedBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    AnonymizedBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    AnonymizedBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    InstallationCode = table.Column<string>(type: "text", nullable: false),
                    AnonymizerWorkflowStatus = table.Column<int>(type: "integer", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalysisToBeRun = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Analysis",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Uri = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InspectionDataId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Analysis_InspectionData_InspectionDataId",
                        column: x => x.InspectionDataId,
                        principalTable: "InspectionData",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analysis_InspectionDataId",
                table: "Analysis",
                column: "InspectionDataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analysis");

            migrationBuilder.DropTable(
                name: "InspectionData");
        }
    }
}
