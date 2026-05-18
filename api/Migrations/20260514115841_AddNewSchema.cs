using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<string>(type: "text", nullable: false),
                    ExpectedSize = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TimeoutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalysisGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Analyses_AnalysisGroups_AnalysisGroupId",
                        column: x => x.AnalysisGroupId,
                        principalTable: "AnalysisGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InspectionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<string>(type: "text", nullable: false),
                    InstallationCode = table.Column<string>(type: "text", nullable: false),
                    BlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: false),
                    BlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: false),
                    BlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InspectionType = table.Column<string>(type: "text", nullable: true),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    TargetPosition_X = table.Column<float>(type: "real", nullable: true),
                    TargetPosition_Y = table.Column<float>(type: "real", nullable: true),
                    TargetPosition_Z = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Position_X = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Position_Y = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Position_Z = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Orientation_X = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Orientation_Y = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Orientation_Z = table.Column<float>(type: "real", nullable: true),
                    RobotPose_Orientation_W = table.Column<float>(type: "real", nullable: true),
                    RobotPose_HasValue = table.Column<bool>(type: "boolean", nullable: true),
                    InspectionDescription = table.Column<string>(type: "text", nullable: true),
                    RobotName = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnalysisGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionRecords_AnalysisGroups_AnalysisGroupId",
                        column: x => x.AnalysisGroupId,
                        principalTable: "AnalysisGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisRuns_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisInspectionRecord",
                columns: table => new
                {
                    AnalysesId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionRecordsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisInspectionRecord", x => new { x.AnalysesId, x.InspectionRecordsId });
                    table.ForeignKey(
                        name: "FK_AnalysisInspectionRecord_Analyses_AnalysesId",
                        column: x => x.AnalysesId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisInspectionRecord_InspectionRecords_InspectionRecord~",
                        column: x => x.InspectionRecordsId,
                        principalTable: "InspectionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    WorkflowType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OutputBlobStorageLocation_StorageAccount = table.Column<string>(type: "text", nullable: true),
                    OutputBlobStorageLocation_BlobContainer = table.Column<string>(type: "text", nullable: true),
                    OutputBlobStorageLocation_BlobName = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workflows_InputBlobStorageLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StorageAccount = table.Column<string>(type: "text", nullable: false),
                    BlobContainer = table.Column<string>(type: "text", nullable: false),
                    BlobName = table.Column<string>(type: "text", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows_InputBlobStorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_InputBlobStorageLocations_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_AnalysisGroupId",
                table: "Analyses",
                column: "AnalysisGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisGroups_GroupId",
                table: "AnalysisGroups",
                column: "GroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisInspectionRecord_InspectionRecordsId",
                table: "AnalysisInspectionRecord",
                column: "InspectionRecordsId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_AnalysisId",
                table: "AnalysisRuns",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecord_CreatedAt_Id_Desc",
                table: "InspectionRecords",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_AnalysisGroupId",
                table: "InspectionRecords",
                column: "AnalysisGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_InspectionId",
                table: "InspectionRecords",
                column: "InspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_AnalysisRunId",
                table: "Workflows",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_InputBlobStorageLocations_WorkflowId",
                table: "Workflows_InputBlobStorageLocations",
                column: "WorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisInspectionRecord");

            migrationBuilder.DropTable(
                name: "Workflows_InputBlobStorageLocations");

            migrationBuilder.DropTable(
                name: "InspectionRecords");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropTable(
                name: "AnalysisRuns");

            migrationBuilder.DropTable(
                name: "Analyses");

            migrationBuilder.DropTable(
                name: "AnalysisGroups");
        }
    }
}
