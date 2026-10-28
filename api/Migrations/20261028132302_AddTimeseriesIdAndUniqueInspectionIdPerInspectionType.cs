using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeseriesIdAndUniqueInspectionIdPerInspectionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionRecords_InspectionId",
                table: "InspectionRecords");

            migrationBuilder.AddColumn<string>(
                name: "TimeseriesId",
                table: "InspectionRecords",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_InspectionId_InspectionType",
                table: "InspectionRecords",
                columns: new[] { "InspectionId", "InspectionType" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionRecords_InspectionId_InspectionType",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "TimeseriesId",
                table: "InspectionRecords");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_InspectionId",
                table: "InspectionRecords",
                column: "InspectionId",
                unique: true);
        }
    }
}
