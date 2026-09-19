using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260727134547_HardenProcurementSupplierApplicantEvidence")]
    public partial class HardenProcurementSupplierApplicantEvidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                columns: new[] { "TenantId", "FileUploadRecordId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerRegistrationDocuments_FileUploadRecords_FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "FileUploadRecordId",
                principalTable: "FileUploadRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_BusinessPartnerRegistrationDocuments_ControlledFileGuard];");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerRegistrationDocuments_FileUploadRecords_FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "TenantId");

            migrationBuilder.DropColumn(
                name: "FileUploadRecordId",
                table: "BusinessPartnerRegistrationDocuments");
        }
    }
}
