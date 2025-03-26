using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Uri",
                table: "Analysis",
                newName: "SourcePath_StorageAccount");

            migrationBuilder.AddColumn<string>(
                name: "MetadataId",
                table: "PlantData",
                type: "text",
                nullable: true);

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

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Analysis",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationPath_BlobContainer",
                table: "Analysis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationPath_BlobName",
                table: "Analysis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationPath_StorageAccount",
                table: "Analysis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePath_BlobContainer",
                table: "Analysis",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourcePath_BlobName",
                table: "Analysis",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AnalysisMapping",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: false),
                    InspectionDescription = table.Column<string>(type: "text", nullable: false),
                    AnalysesToBeRun = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisMapping", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Metadata",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    Coordinates = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_MetadataId",
                table: "PlantData",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisMapping_Tag_InspectionDescription",
                table: "AnalysisMapping",
                columns: new[] { "Tag", "InspectionDescription" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_Metadata_MetadataId",
                table: "PlantData",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_Metadata_MetadataId",
                table: "PlantData");

            migrationBuilder.DropTable(
                name: "AnalysisMapping");

            migrationBuilder.DropTable(
                name: "Metadata");

            migrationBuilder.DropIndex(
                name: "IX_PlantData_MetadataId",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "MetadataId",
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

            migrationBuilder.DropColumn(
                name: "DestinationPath_BlobContainer",
                table: "Analysis");

            migrationBuilder.DropColumn(
                name: "DestinationPath_BlobName",
                table: "Analysis");

            migrationBuilder.DropColumn(
                name: "DestinationPath_StorageAccount",
                table: "Analysis");

            migrationBuilder.DropColumn(
                name: "SourcePath_BlobContainer",
                table: "Analysis");

            migrationBuilder.DropColumn(
                name: "SourcePath_BlobName",
                table: "Analysis");

            migrationBuilder.RenameColumn(
                name: "SourcePath_StorageAccount",
                table: "Analysis",
                newName: "Uri");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Analysis",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
