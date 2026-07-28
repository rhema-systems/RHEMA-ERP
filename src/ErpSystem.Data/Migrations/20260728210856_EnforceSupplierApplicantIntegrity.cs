using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSupplierApplicantIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM [dbo].[ProcurementSupplierApplicantAccesses]
                    WHERE [IsDeleted] = 0
                      AND [Status] IN (0, 1, 2, 5)
                    GROUP BY [TenantId], [VerifiedContactHashSha256]
                    HAVING COUNT_BIG(*) > 1
                )
                BEGIN
                    THROW 51840,
                        'Cannot enforce supplier applicant active-contact uniqueness while duplicate active applications exist.',
                        1;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_ProcurementSupplierApplicantAccesses_ActiveContact",
                table: "ProcurementSupplierApplicantAccesses",
                columns: new[] { "TenantId", "VerifiedContactHashSha256" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN (0, 1, 2, 5)");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_FileUploadRecords_RegistrationEvidenceDeleteGuard]
                ON [dbo].[FileUploadRecords]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF NOT UPDATE([IsDeleted])
                        RETURN;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS currentFile
                        INNER JOIN deleted AS previousFile
                            ON previousFile.[Id] = currentFile.[Id]
                        INNER JOIN [dbo].[BusinessPartnerRegistrationDocuments] AS document
                            ON document.[TenantId] = currentFile.[TenantId]
                           AND document.[FileUploadRecordId] = currentFile.[Id]
                           AND document.[IsDeleted] = 0
                        WHERE previousFile.[IsDeleted] = 0
                          AND currentFile.[IsDeleted] = 1
                    )
                    BEGIN
                        THROW 51841,
                            'A controlled file referenced by active supplier registration evidence cannot be deleted.',
                            1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_FileUploadRecords_RegistrationEvidenceDeleteGuard];");

            migrationBuilder.DropIndex(
                name: "UX_ProcurementSupplierApplicantAccesses_ActiveContact",
                table: "ProcurementSupplierApplicantAccesses");
        }
    }
}
