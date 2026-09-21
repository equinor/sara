using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisResultValuesAndThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisResultValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnalysisType = table.Column<string>(type: "text", nullable: false),
                    InstallationCode = table.Column<string>(type: "text", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    InspectionDescription = table.Column<string>(type: "text", nullable: true),
                    CorrelationKey = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    ValueKind = table.Column<string>(type: "text", nullable: false),
                    NumericValue = table.Column<double>(type: "double precision", nullable: true),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    TextValue = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    ModelMessage = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    ThresholdSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    Acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResultValues", x => x.Id);
                    table.CheckConstraint("CK_AnalysisResultValue_ExactlyOneValue", "(CASE WHEN \"NumericValue\" IS NULL THEN 0 ELSE 1 END\n + CASE WHEN \"BooleanValue\" IS NULL THEN 0 ELSE 1 END\n + CASE WHEN \"TextValue\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_AnalysisResultValues_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    LowerAlert = table.Column<double>(type: "double precision", nullable: true),
                    LowerWarning = table.Column<double>(type: "double precision", nullable: true),
                    UpperWarning = table.Column<double>(type: "double precision", nullable: true),
                    UpperAlert = table.Column<double>(type: "double precision", nullable: true),
                    AlertWhenTrue = table.Column<bool>(type: "boolean", nullable: true),
                    MinConfidence = table.Column<double>(type: "double precision", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisThresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisThresholds_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResultValue_Correlation_MeasuredAt_Desc",
                table: "AnalysisResultValues",
                columns: new[] { "CorrelationKey", "MeasuredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResultValues_AnalysisRunId",
                table: "AnalysisResultValues",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisThresholds_AnalysisId_Key",
                table: "AnalysisThresholds",
                columns: new[] { "AnalysisId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisResultValues");

            migrationBuilder.DropTable(
                name: "AnalysisThresholds");
        }
    }
}
