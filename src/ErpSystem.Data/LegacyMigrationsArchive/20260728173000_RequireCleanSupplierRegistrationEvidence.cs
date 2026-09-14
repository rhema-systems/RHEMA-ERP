using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728173000_RequireCleanSupplierRegistrationEvidence")]
    public partial class RequireCleanSupplierRegistrationEvidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_BusinessPartnerRegistrationDocuments_ControlledFileGuard]
                ON [dbo].[BusinessPartnerRegistrationDocuments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS document
                        LEFT JOIN [dbo].[FileUploadRecords] AS controlledFile
                            ON controlledFile.[Id] = document.[FileUploadRecordId]
                        WHERE document.[FileUploadRecordId] IS NOT NULL
                          AND
                          (
                              controlledFile.[Id] IS NULL
                              OR controlledFile.[TenantId] <> document.[TenantId]
                              OR controlledFile.[IsDeleted] = 1
                              OR controlledFile.[VirusScanStatus] <> 2
                          )
                    )
                    BEGIN
                        THROW 51839,
                            'Registration evidence must reference an active same-tenant controlled file with a clean virus-scan result.',
                            1;
                    END;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_BusinessPartnerRegistrationDocuments_ControlledFileGuard]
                ON [dbo].[BusinessPartnerRegistrationDocuments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS document
                        LEFT JOIN [dbo].[FileUploadRecords] AS controlledFile
                            ON controlledFile.[Id] = document.[FileUploadRecordId]
                        WHERE document.[FileUploadRecordId] IS NOT NULL
                          AND
                          (
                              controlledFile.[Id] IS NULL
                              OR controlledFile.[TenantId] <> document.[TenantId]
                              OR controlledFile.[IsDeleted] = 1
                              OR controlledFile.[VirusScanStatus] NOT IN (0, 2)
                          )
                    )
                    BEGIN
                        THROW 51839,
                            'Registration evidence must reference an active same-tenant controlled file with a permitted virus-scan outcome.',
                            1;
                    END;
                END;
                """);
        }
    }
}
