using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <summary>
    /// Data-only migration: analysis types are now stored as the <c>AnalysisTypeEnum</c>
    /// member names shared with Flotilla (e.g. "ThermalReading") instead of the
    /// SARA-specific kebab-case keys. Workflow types are not affected.
    /// </summary>
    public partial class RenameAnalysisTypesToEnumNames : Migration
    {
        private static readonly (string Legacy, string EnumName)[] Renames =
        [
            ("fencilla", "Fencilla"),
            ("cloe", "CLOE"),
            ("thermal-reading", "ThermalReading"),
            ("co2", "CO2"),
            ("anonymize", "Anonymize"),
            ("passthrough", "Passthrough"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (legacy, enumName) in Renames)
            {
                migrationBuilder.Sql(
                    $"""UPDATE "Analyses" SET "AnalysisType" = '{enumName}' WHERE "AnalysisType" = '{legacy}';"""
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (legacy, enumName) in Renames)
            {
                migrationBuilder.Sql(
                    $"""UPDATE "Analyses" SET "AnalysisType" = '{legacy}' WHERE "AnalysisType" = '{enumName}';"""
                );
            }
        }
    }
}
