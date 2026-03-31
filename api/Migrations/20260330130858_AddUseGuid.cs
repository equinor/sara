using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddUseGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FK constraints before altering column types
            migrationBuilder.Sql(@"
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_Anonymization_AnonymizationId"";
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_CLOEAnalysis_CLOEAnalysisId"";
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_FencillaAnalysis_FencillaAnalysisId"";
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_ThermalReading_ThermalReadingAnalysisId"";
            ");

            // Convert all ID and FK columns from text to uuid using explicit USING cast
            migrationBuilder.Sql(@"
                ALTER TABLE ""PlantData"" ALTER COLUMN ""AnonymizationId"" DROP DEFAULT;
                ALTER TABLE ""AnalysisMapping"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                ALTER TABLE ""Anonymization"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                ALTER TABLE ""CLOEAnalysis"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                ALTER TABLE ""FencillaAnalysis"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                ALTER TABLE ""ThermalReading"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""AnonymizationId"" TYPE uuid USING ""AnonymizationId""::uuid;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""CLOEAnalysisId"" TYPE uuid USING ""CLOEAnalysisId""::uuid;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""FencillaAnalysisId"" TYPE uuid USING ""FencillaAnalysisId""::uuid;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""ThermalReadingAnalysisId"" TYPE uuid USING ""ThermalReadingAnalysisId""::uuid;
            ");

            // Re-add FK constraints
            migrationBuilder.Sql(@"
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_Anonymization_AnonymizationId""
                    FOREIGN KEY (""AnonymizationId"") REFERENCES ""Anonymization"" (""Id"");
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_CLOEAnalysis_CLOEAnalysisId""
                    FOREIGN KEY (""CLOEAnalysisId"") REFERENCES ""CLOEAnalysis"" (""Id"");
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_FencillaAnalysis_FencillaAnalysisId""
                    FOREIGN KEY (""FencillaAnalysisId"") REFERENCES ""FencillaAnalysis"" (""Id"");
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_ThermalReading_ThermalReadingAnalysisId""
                    FOREIGN KEY (""ThermalReadingAnalysisId"") REFERENCES ""ThermalReading"" (""Id"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK constraints before altering column types
            migrationBuilder.Sql(@"
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_Anonymization_AnonymizationId"";
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_CLOEAnalysis_CLOEAnalysisId"";
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_FencillaAnalysis_FencillaAnalysisId"";
                ALTER TABLE ""PlantData"" DROP CONSTRAINT IF EXISTS ""FK_PlantData_ThermalReading_ThermalReadingAnalysisId"";
            ");

            // Convert all columns back from uuid to text
            migrationBuilder.Sql(@"
                ALTER TABLE ""AnalysisMapping"" ALTER COLUMN ""Id"" TYPE text USING ""Id""::text;
                ALTER TABLE ""Anonymization"" ALTER COLUMN ""Id"" TYPE text USING ""Id""::text;
                ALTER TABLE ""CLOEAnalysis"" ALTER COLUMN ""Id"" TYPE text USING ""Id""::text;
                ALTER TABLE ""FencillaAnalysis"" ALTER COLUMN ""Id"" TYPE text USING ""Id""::text;
                ALTER TABLE ""ThermalReading"" ALTER COLUMN ""Id"" TYPE text USING ""Id""::text;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""Id"" TYPE text USING ""Id""::text;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""AnonymizationId"" TYPE text USING ""AnonymizationId""::text;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""CLOEAnalysisId"" TYPE text USING ""CLOEAnalysisId""::text;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""FencillaAnalysisId"" TYPE text USING ""FencillaAnalysisId""::text;
                ALTER TABLE ""PlantData"" ALTER COLUMN ""ThermalReadingAnalysisId"" TYPE text USING ""ThermalReadingAnalysisId""::text;
            ");

            // Re-add FK constraints
            migrationBuilder.Sql(@"
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_Anonymization_AnonymizationId""
                    FOREIGN KEY (""AnonymizationId"") REFERENCES ""Anonymization"" (""Id"");
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_CLOEAnalysis_CLOEAnalysisId""
                    FOREIGN KEY (""CLOEAnalysisId"") REFERENCES ""CLOEAnalysis"" (""Id"");
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_FencillaAnalysis_FencillaAnalysisId""
                    FOREIGN KEY (""FencillaAnalysisId"") REFERENCES ""FencillaAnalysis"" (""Id"");
                ALTER TABLE ""PlantData"" ADD CONSTRAINT ""FK_PlantData_ThermalReading_ThermalReadingAnalysisId""
                    FOREIGN KEY (""ThermalReadingAnalysisId"") REFERENCES ""ThermalReading"" (""Id"");
            ");
        }
    }
}
