using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceAnalysisTypeToReferencePolygonMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceAnalysisType",
                table: "ReferencePolygonMetadata",
                type: "text",
                nullable: true);

            // All pre-existing rows are thermal reference images; backfill
            // before making the column non-nullable so they aren't defaulted
            // to an empty string.
            migrationBuilder.Sql(
                """
                UPDATE "ReferencePolygonMetadata"
                SET "SourceAnalysisType" = 'ThermalReading';
                """
            );

            migrationBuilder.AlterColumn<string>(
                name: "SourceAnalysisType",
                table: "ReferencePolygonMetadata",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceAnalysisType",
                table: "ReferencePolygonMetadata");
        }
    }
}
