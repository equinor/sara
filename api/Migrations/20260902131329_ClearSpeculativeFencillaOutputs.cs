using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class ClearSpeculativeFencillaOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Workflows"
                SET "OutputBlobStorageLocation_StorageAccount" = NULL,
                    "OutputBlobStorageLocation_BlobContainer" = NULL,
                    "OutputBlobStorageLocation_BlobName" = NULL
                WHERE "WorkflowType" = 'fencilla'
                  AND "Status" = 2
                  AND "ResultJson" IS NOT NULL
                  AND "ResultJson" NOT LIKE '%"outputBlobStorageLocation"%';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Speculative locations cannot be reconstructed safely.
        }
    }
}
