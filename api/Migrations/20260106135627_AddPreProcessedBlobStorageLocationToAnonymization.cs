using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddPreProcessedBlobStorageLocationToAnonymization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreProcessedBlobStorageLocation_BlobContainer",
                table: "Anonymization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreProcessedBlobStorageLocation_BlobName",
                table: "Anonymization",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreProcessedBlobStorageLocation_StorageAccount",
                table: "Anonymization",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreProcessedBlobStorageLocation_BlobContainer",
                table: "Anonymization");

            migrationBuilder.DropColumn(
                name: "PreProcessedBlobStorageLocation_BlobName",
                table: "Anonymization");

            migrationBuilder.DropColumn(
                name: "PreProcessedBlobStorageLocation_StorageAccount",
                table: "Anonymization");
        }
    }
}
