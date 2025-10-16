using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantData_Metadata_MetadataId",
                table: "PlantData");

            migrationBuilder.DropTable(
                name: "Metadata");

            migrationBuilder.DropIndex(
                name: "IX_PlantData_MetadataId",
                table: "PlantData");

            migrationBuilder.RenameColumn(
                name: "MetadataId",
                table: "PlantData",
                newName: "Tag");

            migrationBuilder.AddColumn<string>(
                name: "Coordinates",
                table: "PlantData",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionDescription",
                table: "PlantData",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "PlantData",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coordinates",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "InspectionDescription",
                table: "PlantData");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "PlantData");

            migrationBuilder.RenameColumn(
                name: "Tag",
                table: "PlantData",
                newName: "MetadataId");

            migrationBuilder.CreateTable(
                name: "Metadata",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: true),
                    Tag = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantData_MetadataId",
                table: "PlantData",
                column: "MetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantData_Metadata_MetadataId",
                table: "PlantData",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id");
        }
    }
}
