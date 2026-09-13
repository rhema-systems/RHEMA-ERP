using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementPrequalificationPolicyLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM dbo.ProcurementPrequalificationExercises)
                    THROW 51163, 'Policy lineage migration requires an empty prequalification exercise register; no guessed backfill is permitted.', 1;
                """);

            migrationBuilder.AddColumn<string>(
                name: "PolicySetCode",
                table: "ProcurementPrequalificationExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PolicySetId",
                table: "ProcurementPrequalificationExercises",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "PolicySetVersion",
                table: "ProcurementPrequalificationExercises",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceConfigurationProfileId",
                table: "ProcurementPrequalificationExercises",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_PolicySetId",
                table: "ProcurementPrequalificationExercises",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_TenantId_PolicySetId",
                table: "ProcurementPrequalificationExercises",
                columns: new[] { "TenantId", "PolicySetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPrequalificationExercises_ProcurementPolicySets_PolicySetId",
                table: "ProcurementPrequalificationExercises",
                column: "PolicySetId",
                principalTable: "ProcurementPolicySets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER dbo.TR_ProcurementPrequalificationExercises_PolicyLineage
                ON dbo.ProcurementPrequalificationExercises
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS i
                        LEFT JOIN dbo.ProcurementPolicySets AS p
                            ON p.Id = i.PolicySetId
                           AND p.TenantId = i.TenantId
                           AND p.IsDeleted = 0
                        WHERE p.Id IS NULL
                           OR p.Code <> i.PolicySetCode
                           OR p.Version <> i.PolicySetVersion
                           OR p.SourceConfigurationProfileId <> i.SourceConfigurationProfileId
                    )
                        THROW 51164, 'Prequalification policy lineage must match an undeleted policy in the same tenant.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS i
                        INNER JOIN deleted AS d ON d.Id = i.Id
                        WHERE i.PolicySetId <> d.PolicySetId
                           OR i.PolicySetCode <> d.PolicySetCode
                           OR i.PolicySetVersion <> d.PolicySetVersion
                           OR i.SourceConfigurationProfileId <> d.SourceConfigurationProfileId
                    )
                        THROW 51165, 'Prequalification policy lineage is immutable.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS dbo.TR_ProcurementPrequalificationExercises_PolicyLineage;");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPrequalificationExercises_ProcurementPolicySets_PolicySetId",
                table: "ProcurementPrequalificationExercises");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPrequalificationExercises_PolicySetId",
                table: "ProcurementPrequalificationExercises");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPrequalificationExercises_TenantId_PolicySetId",
                table: "ProcurementPrequalificationExercises");

            migrationBuilder.DropColumn(
                name: "PolicySetCode",
                table: "ProcurementPrequalificationExercises");

            migrationBuilder.DropColumn(
                name: "PolicySetId",
                table: "ProcurementPrequalificationExercises");

            migrationBuilder.DropColumn(
                name: "PolicySetVersion",
                table: "ProcurementPrequalificationExercises");

            migrationBuilder.DropColumn(
                name: "SourceConfigurationProfileId",
                table: "ProcurementPrequalificationExercises");
        }
    }
}
