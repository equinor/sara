using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RenameInspectionDataToPlantData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Analysis_InspectionData_InspectionDataId",
                table: "Analysis");

            migrationBuilder.RenameTable(
                name: "InspectionData",
                newName: "PlantData");

            migrationBuilder.RenameColumn(
                name: "InspectionDataId",
                table: "Analysis",
                newName: "PlantDataId");

            migrationBuilder.RenameIndex(
                name: "IX_Analysis_InspectionDataId",
                table: "Analysis",
                newName: "IX_Analysis_PlantDataId");

            migrationBuilder.AddForeignKey(
                name: "FK_Analysis_PlantData_PlantDataId",
                table: "Analysis",
                column: "PlantDataId",
                principalTable: "PlantData",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Analysis_PlantData_PlantDataId",
                table: "Analysis");

            migrationBuilder.RenameTable(
                name: "PlantData",
                newName: "InspectionData");

            migrationBuilder.RenameColumn(
                name: "PlantDataId",
                table: "Analysis",
                newName: "InspectionDataId");

            migrationBuilder.RenameIndex(
                name: "IX_Analysis_PlantDataId",
                table: "Analysis",
                newName: "IX_Analysis_InspectionDataId");

            migrationBuilder.AddForeignKey(
                name: "FK_Analysis_InspectionData_InspectionDataId",
                table: "Analysis",
                column: "InspectionDataId",
                principalTable: "InspectionData",
                principalColumn: "Id");
        }
    }
}
