using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Copies data from the legacy PlantData / Anonymization /
    /// CLOEAnalysis / FencillaAnalysis / ThermalReading tables into the
    /// new InspectionRecord / Analysis / AnalysisRun / Workflow schema
    /// created by <see cref="AddNewSchema"/>. The legacy tables are
    /// dropped by <see cref="DropLegacyTables"/>.
    ///
    /// Mapping rules (per PlantData row):
    ///   - PlantData.Id is preserved as InspectionRecord.Id.
    ///   - Anonymization.SourceBlobStorageLocation_* becomes
    ///     InspectionRecord.BlobStorageLocation_*.
    ///   - One Analysis is created per PlantData row, named after the
    ///     terminal workflow ("anonymize" if no downstream, otherwise
    ///     "cloe" / "fencilla" / "thermal-reading").
    ///   - One AnalysisRun is created per Analysis with RunNumber=1.
    ///   - Step 1 Workflow is "anonymizer" with Anonymization.Id
    ///     preserved as Workflow.Id.
    ///   - Step 2 Workflow is the downstream type with the downstream
    ///     row's Id preserved as Workflow.Id.
    ///   - Workflow.Status maps ExitSuccess->Succeeded(2),
    ///     ExitFailure->Failed(3), and NotStarted/Started->Failed(3)
    ///     with ErrorMessage explaining the demotion.
    ///   - AnalysisRun.Status is Succeeded(2) iff every workflow in the
    ///     run is Succeeded, else Failed(3).
    ///   - Workflow.ResultJson is populated only when Status=Succeeded
    ///     AND the metric column is non-null, using the camelCase shape
    ///     emitted by the modern result handlers.
    ///   - Coordinates is intentionally dropped (100% NULL on dev data).
    ///
    /// This migration is intentionally one-way; <see cref="Down"/>
    /// throws because reconstructing the legacy schema's per-type
    /// tables from the merged Workflow rows is not supported.
    /// </remarks>
    public partial class MigrateLegacyPlantData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- =====================================================
                -- Step 0: Materialize per-PlantData identifiers and the
                -- terminal workflow type into a temporary work table.
                -- The temp table is dropped automatically when the
                -- migration transaction commits.
                -- =====================================================
                CREATE TEMP TABLE _migration_ids ON COMMIT DROP AS
                SELECT
                    pd."Id"                AS plant_data_id,
                    pd."Id"                AS inspection_record_id,
                    gen_random_uuid()      AS analysis_id,
                    gen_random_uuid()      AS analysis_run_id,
                    pd."AnonymizationId"   AS anonymization_id,
                    pd."CLOEAnalysisId"    AS cloe_id,
                    pd."FencillaAnalysisId" AS fencilla_id,
                    pd."ThermalReadingAnalysisId" AS thermal_id,
                    pd."DateCreated"       AS created_at,
                    CASE
                        WHEN pd."CLOEAnalysisId" IS NOT NULL THEN 'cloe'
                        WHEN pd."FencillaAnalysisId" IS NOT NULL THEN 'fencilla'
                        WHEN pd."ThermalReadingAnalysisId" IS NOT NULL THEN 'thermal-reading'
                        ELSE 'anonymize'
                    END                    AS analysis_name
                FROM "PlantData" pd;

                CREATE INDEX ON _migration_ids (plant_data_id);
                CREATE INDEX ON _migration_ids (anonymization_id);
                CREATE INDEX ON _migration_ids (cloe_id);
                CREATE INDEX ON _migration_ids (fencilla_id);
                CREATE INDEX ON _migration_ids (thermal_id);

                -- =====================================================
                -- Step 1: Insert InspectionRecord rows. The blob
                -- location comes from the anonymizer source (i.e. the
                -- robot's original capture). Pose / target / inspection
                -- type are not present in the legacy schema and remain
                -- NULL.
                -- =====================================================
                INSERT INTO "InspectionRecords" (
                    "Id",
                    "InspectionId",
                    "InstallationCode",
                    "BlobStorageLocation_StorageAccount",
                    "BlobStorageLocation_BlobContainer",
                    "BlobStorageLocation_BlobName",
                    "CreatedAt",
                    "InspectionType",
                    "Tag",
                    "InspectionDescription",
                    "RobotName",
                    "Timestamp",
                    "AnalysisGroupId"
                )
                SELECT
                    ids.inspection_record_id,
                    pd."InspectionId",
                    pd."InstallationCode",
                    anon."SourceBlobStorageLocation_StorageAccount",
                    anon."SourceBlobStorageLocation_BlobContainer",
                    anon."SourceBlobStorageLocation_BlobName",
                    pd."DateCreated",
                    NULL,
                    pd."Tag",
                    pd."InspectionDescription",
                    pd."RobotName",
                    pd."Timestamp",
                    NULL
                FROM _migration_ids ids
                JOIN "PlantData" pd       ON pd."Id" = ids.plant_data_id
                JOIN "Anonymization" anon ON anon."Id" = ids.anonymization_id;

                -- =====================================================
                -- Step 2: Insert Analysis rows (one per InspectionRecord).
                -- =====================================================
                INSERT INTO "Analyses" ("Id", "Name", "CreatedAt", "AnalysisGroupId")
                SELECT
                    ids.analysis_id,
                    ids.analysis_name,
                    ids.created_at,
                    NULL
                FROM _migration_ids ids;

                -- =====================================================
                -- Step 3: Link each Analysis to its InspectionRecord
                -- via the join table.
                -- =====================================================
                INSERT INTO "AnalysisInspectionRecord" ("AnalysesId", "InspectionRecordsId")
                SELECT ids.analysis_id, ids.inspection_record_id
                FROM _migration_ids ids;

                -- =====================================================
                -- Step 4: Insert AnalysisRun rows. Status is computed
                -- as Succeeded(2) iff every workflow in the run was
                -- ExitSuccess(2) in the legacy schema; otherwise
                -- Failed(3). A single anonymizer-only run uses the
                -- anonymizer's status; a two-step run requires both.
                -- =====================================================
                INSERT INTO "AnalysisRuns" (
                    "Id", "AnalysisId", "RunNumber", "Status", "StartedAt", "CompletedAt"
                )
                SELECT
                    ids.analysis_run_id,
                    ids.analysis_id,
                    1,
                    CASE
                        WHEN anon."Status" = 2
                            AND COALESCE(cloe."Status", 2) = 2
                            AND COALESCE(fenc."Status", 2) = 2
                            AND COALESCE(therm."Status", 2) = 2
                        THEN 2
                        ELSE 3
                    END,
                    ids.created_at,
                    ids.created_at
                FROM _migration_ids ids
                JOIN "Anonymization" anon          ON anon."Id" = ids.anonymization_id
                LEFT JOIN "CLOEAnalysis" cloe       ON cloe."Id" = ids.cloe_id
                LEFT JOIN "FencillaAnalysis" fenc   ON fenc."Id" = ids.fencilla_id
                LEFT JOIN "ThermalReading" therm    ON therm."Id" = ids.thermal_id;

                -- =====================================================
                -- Step 5a: Insert step-1 Workflow ("anonymizer") for
                -- every PlantData row. The Workflow.Id preserves the
                -- legacy Anonymization.Id so external correlation IDs
                -- (e.g. MQTT message references) still resolve.
                --
                -- Status mapping: ExitSuccess(2) -> Succeeded(2),
                -- ExitFailure(3) -> Failed(3), and NotStarted(0) /
                -- Started(1) -> Failed(3) with explanatory ErrorMessage.
                --
                -- ResultJson is emitted only when Status=Succeeded AND
                -- the metric column (IsPersonInImage) is non-null. The
                -- shape matches the modern Anonymizer result handler.
                -- The OutputBlobStorageLocation comes from the legacy
                -- DestinationBlobStorageLocation columns.
                -- =====================================================
                INSERT INTO "Workflows" (
                    "Id",
                    "AnalysisRunId",
                    "StepNumber",
                    "WorkflowType",
                    "Status",
                    "OutputBlobStorageLocation_StorageAccount",
                    "OutputBlobStorageLocation_BlobContainer",
                    "OutputBlobStorageLocation_BlobName",
                    "ResultJson",
                    "StartedAt",
                    "CompletedAt",
                    "ErrorMessage"
                )
                SELECT
                    anon."Id",
                    ids.analysis_run_id,
                    1,
                    'anonymizer',
                    CASE WHEN anon."Status" IN (0, 1) THEN 3 ELSE anon."Status" END,
                    anon."DestinationBlobStorageLocation_StorageAccount",
                    anon."DestinationBlobStorageLocation_BlobContainer",
                    anon."DestinationBlobStorageLocation_BlobName",
                    CASE
                        WHEN anon."Status" = 2 AND anon."IsPersonInImage" IS NOT NULL THEN
                            json_build_object(
                                'isPersonInImage', anon."IsPersonInImage",
                                'outputBlobStorageLocation', json_build_object(
                                    'storageAccount', anon."DestinationBlobStorageLocation_StorageAccount",
                                    'blobContainer',  anon."DestinationBlobStorageLocation_BlobContainer",
                                    'blobName',       anon."DestinationBlobStorageLocation_BlobName"
                                ),
                                'preProcessedBlobStorageLocation',
                                CASE WHEN anon."PreProcessedBlobStorageLocation_StorageAccount" IS NOT NULL THEN
                                    json_build_object(
                                        'storageAccount', anon."PreProcessedBlobStorageLocation_StorageAccount",
                                        'blobContainer',  anon."PreProcessedBlobStorageLocation_BlobContainer",
                                        'blobName',       anon."PreProcessedBlobStorageLocation_BlobName"
                                    )
                                ELSE NULL::json END
                            )::text
                        ELSE NULL
                    END,
                    ids.created_at,
                    ids.created_at,
                    CASE WHEN anon."Status" IN (0, 1)
                         THEN 'Workflow lost during schema migration'
                         ELSE NULL END
                FROM _migration_ids ids
                JOIN "Anonymization" anon ON anon."Id" = ids.anonymization_id;

                -- =====================================================
                -- Step 5b: Insert step-2 Workflow for CLOE chains.
                -- =====================================================
                INSERT INTO "Workflows" (
                    "Id",
                    "AnalysisRunId",
                    "StepNumber",
                    "WorkflowType",
                    "Status",
                    "OutputBlobStorageLocation_StorageAccount",
                    "OutputBlobStorageLocation_BlobContainer",
                    "OutputBlobStorageLocation_BlobName",
                    "ResultJson",
                    "StartedAt",
                    "CompletedAt",
                    "ErrorMessage"
                )
                SELECT
                    cloe."Id",
                    ids.analysis_run_id,
                    2,
                    'cloe',
                    CASE WHEN cloe."Status" IN (0, 1) THEN 3 ELSE cloe."Status" END,
                    cloe."DestinationBlobStorageLocation_StorageAccount",
                    cloe."DestinationBlobStorageLocation_BlobContainer",
                    cloe."DestinationBlobStorageLocation_BlobName",
                    CASE
                        WHEN cloe."Status" = 2
                             AND cloe."OilLevel" IS NOT NULL
                             AND cloe."Confidence" IS NOT NULL THEN
                            json_build_object(
                                'oilLevel',   cloe."OilLevel",
                                'confidence', cloe."Confidence"
                            )::text
                        ELSE NULL
                    END,
                    ids.created_at,
                    ids.created_at,
                    CASE WHEN cloe."Status" IN (0, 1)
                         THEN 'Workflow lost during schema migration'
                         ELSE NULL END
                FROM _migration_ids ids
                JOIN "CLOEAnalysis" cloe ON cloe."Id" = ids.cloe_id;

                -- =====================================================
                -- Step 5c: Insert step-2 Workflow for Fencilla chains.
                -- =====================================================
                INSERT INTO "Workflows" (
                    "Id",
                    "AnalysisRunId",
                    "StepNumber",
                    "WorkflowType",
                    "Status",
                    "OutputBlobStorageLocation_StorageAccount",
                    "OutputBlobStorageLocation_BlobContainer",
                    "OutputBlobStorageLocation_BlobName",
                    "ResultJson",
                    "StartedAt",
                    "CompletedAt",
                    "ErrorMessage"
                )
                SELECT
                    fenc."Id",
                    ids.analysis_run_id,
                    2,
                    'fencilla',
                    CASE WHEN fenc."Status" IN (0, 1) THEN 3 ELSE fenc."Status" END,
                    fenc."DestinationBlobStorageLocation_StorageAccount",
                    fenc."DestinationBlobStorageLocation_BlobContainer",
                    fenc."DestinationBlobStorageLocation_BlobName",
                    CASE
                        WHEN fenc."Status" = 2
                             AND fenc."IsBreak" IS NOT NULL
                             AND fenc."Confidence" IS NOT NULL THEN
                            json_build_object(
                                'isBreak',    fenc."IsBreak",
                                'confidence', fenc."Confidence"
                            )::text
                        ELSE NULL
                    END,
                    ids.created_at,
                    ids.created_at,
                    CASE WHEN fenc."Status" IN (0, 1)
                         THEN 'Workflow lost during schema migration'
                         ELSE NULL END
                FROM _migration_ids ids
                JOIN "FencillaAnalysis" fenc ON fenc."Id" = ids.fencilla_id;

                -- =====================================================
                -- Step 5d: Insert step-2 Workflow for Thermal chains.
                -- =====================================================
                INSERT INTO "Workflows" (
                    "Id",
                    "AnalysisRunId",
                    "StepNumber",
                    "WorkflowType",
                    "Status",
                    "OutputBlobStorageLocation_StorageAccount",
                    "OutputBlobStorageLocation_BlobContainer",
                    "OutputBlobStorageLocation_BlobName",
                    "ResultJson",
                    "StartedAt",
                    "CompletedAt",
                    "ErrorMessage"
                )
                SELECT
                    therm."Id",
                    ids.analysis_run_id,
                    2,
                    'thermal-reading',
                    CASE WHEN therm."Status" IN (0, 1) THEN 3 ELSE therm."Status" END,
                    therm."DestinationBlobStorageLocation_StorageAccount",
                    therm."DestinationBlobStorageLocation_BlobContainer",
                    therm."DestinationBlobStorageLocation_BlobName",
                    CASE
                        WHEN therm."Status" = 2 AND therm."Temperature" IS NOT NULL THEN
                            json_build_object(
                                'temperature', therm."Temperature"
                            )::text
                        ELSE NULL
                    END,
                    ids.created_at,
                    ids.created_at,
                    CASE WHEN therm."Status" IN (0, 1)
                         THEN 'Workflow lost during schema migration'
                         ELSE NULL END
                FROM _migration_ids ids
                JOIN "ThermalReading" therm ON therm."Id" = ids.thermal_id;

                -- =====================================================
                -- Step 6: Insert the Workflow input blob locations
                -- (an owned collection on Workflow). Step-1 inputs are
                -- the anonymizer's source (= the InspectionRecord blob).
                -- Step-2 inputs are the downstream's source (which in
                -- the legacy schema equals the anonymizer's destination).
                -- The synthetic `Id` column is identity-by-default and
                -- generated automatically.
                -- =====================================================
                INSERT INTO "Workflows_InputBlobStorageLocations" (
                    "WorkflowId", "StorageAccount", "BlobContainer", "BlobName"
                )
                SELECT
                    anon."Id",
                    anon."SourceBlobStorageLocation_StorageAccount",
                    anon."SourceBlobStorageLocation_BlobContainer",
                    anon."SourceBlobStorageLocation_BlobName"
                FROM _migration_ids ids
                JOIN "Anonymization" anon ON anon."Id" = ids.anonymization_id;

                INSERT INTO "Workflows_InputBlobStorageLocations" (
                    "WorkflowId", "StorageAccount", "BlobContainer", "BlobName"
                )
                SELECT
                    cloe."Id",
                    cloe."SourceBlobStorageLocation_StorageAccount",
                    cloe."SourceBlobStorageLocation_BlobContainer",
                    cloe."SourceBlobStorageLocation_BlobName"
                FROM _migration_ids ids
                JOIN "CLOEAnalysis" cloe ON cloe."Id" = ids.cloe_id;

                INSERT INTO "Workflows_InputBlobStorageLocations" (
                    "WorkflowId", "StorageAccount", "BlobContainer", "BlobName"
                )
                SELECT
                    fenc."Id",
                    fenc."SourceBlobStorageLocation_StorageAccount",
                    fenc."SourceBlobStorageLocation_BlobContainer",
                    fenc."SourceBlobStorageLocation_BlobName"
                FROM _migration_ids ids
                JOIN "FencillaAnalysis" fenc ON fenc."Id" = ids.fencilla_id;

                INSERT INTO "Workflows_InputBlobStorageLocations" (
                    "WorkflowId", "StorageAccount", "BlobContainer", "BlobName"
                )
                SELECT
                    therm."Id",
                    therm."SourceBlobStorageLocation_StorageAccount",
                    therm."SourceBlobStorageLocation_BlobContainer",
                    therm."SourceBlobStorageLocation_BlobName"
                FROM _migration_ids ids
                JOIN "ThermalReading" therm ON therm."Id" = ids.thermal_id;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "MigrateLegacyPlantData cannot be reversed. The legacy "
                    + "per-type analysis tables cannot be reconstructed from "
                    + "the merged Workflow rows."
            );
        }
    }
}
