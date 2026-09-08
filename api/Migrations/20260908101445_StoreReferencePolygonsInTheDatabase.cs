using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class StoreReferencePolygonsInTheDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferencePolygonBlobStorageLocation_BlobContainer",
                table: "ReferencePolygonMetadata");

            migrationBuilder.DropColumn(
                name: "ReferencePolygonBlobStorageLocation_BlobName",
                table: "ReferencePolygonMetadata");

            migrationBuilder.RenameColumn(
                name: "ReferencePolygonBlobStorageLocation_StorageAccount",
                table: "ReferencePolygonMetadata",
                newName: "Polygon");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Polygon",
                table: "ReferencePolygonMetadata",
                newName: "ReferencePolygonBlobStorageLocation_StorageAccount");

            migrationBuilder.AddColumn<string>(
                name: "ReferencePolygonBlobStorageLocation_BlobContainer",
                table: "ReferencePolygonMetadata",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferencePolygonBlobStorageLocation_BlobName",
                table: "ReferencePolygonMetadata",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
