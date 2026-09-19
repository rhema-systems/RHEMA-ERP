using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds TDC's controlled budget-virement and supplementary-budget register.
///
/// This migration is deliberately hand-scoped after scaffolding because the shared model snapshot
/// also contains unrelated foreign-exchange precision drift. Keeping this migration Finance-budget
/// specific prevents a budget deployment from silently changing settlement calculations.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808175914_AddFinanceBudgetRevisionControls")]
public sealed partial class AddFinanceBudgetRevisionControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Version lineage preserves the original approved budget and every approved successor.
        migrationBuilder.AddColumn<Guid>(
            name: "ParentScenarioId",
            table: "BudgetScenarios",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "VersionNumber",
            table: "BudgetScenarios",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "VersionType",
            table: "BudgetScenarios",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Original");

        migrationBuilder.CreateTable(
            name: "BudgetRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RevisionNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                RevisionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SourceScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ResultScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                BoardResolutionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BoardResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BudgetRevisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_BudgetRevisions_BudgetScenarios_ResultScenarioId",
                    column: x => x.ResultScenarioId,
                    principalTable: "BudgetScenarios",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_BudgetRevisions_BudgetScenarios_SourceScenarioId",
                    column: x => x.SourceScenarioId,
                    principalTable: "BudgetScenarios",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_BudgetRevisions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "BudgetRevisionLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BudgetRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SegmentValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AdjustmentAmountBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BudgetRevisionLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_BudgetRevisionLines_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BudgetRevisionLines_BudgetRevisions_BudgetRevisionId",
                    column: x => x.BudgetRevisionId,
                    principalTable: "BudgetRevisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BudgetRevisionLines_FiscalPeriods_FiscalPeriodId",
                    column: x => x.FiscalPeriodId,
                    principalTable: "FiscalPeriods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BudgetRevisionLines_SegmentLookupValues_SegmentValueId",
                    column: x => x.SegmentValueId,
                    principalTable: "SegmentLookupValues",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_BudgetRevisionLines_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BudgetScenarios_ParentScenarioId",
            table: "BudgetScenarios",
            column: "ParentScenarioId");

        migrationBuilder.CreateIndex(name: "IX_BudgetRevisionLines_AccountId", table: "BudgetRevisionLines", column: "AccountId");
        migrationBuilder.CreateIndex(name: "IX_BudgetRevisionLines_BudgetRevisionId", table: "BudgetRevisionLines", column: "BudgetRevisionId");
        migrationBuilder.CreateIndex(name: "IX_BudgetRevisionLines_FiscalPeriodId", table: "BudgetRevisionLines", column: "FiscalPeriodId");
        migrationBuilder.CreateIndex(name: "IX_BudgetRevisionLines_SegmentValueId", table: "BudgetRevisionLines", column: "SegmentValueId");
        migrationBuilder.CreateIndex(
            name: "IX_BudgetRevisionLines_TenantId_BudgetRevisionId_SegmentValueId_AccountId_FiscalPeriodId",
            table: "BudgetRevisionLines",
            columns: new[] { "TenantId", "BudgetRevisionId", "SegmentValueId", "AccountId", "FiscalPeriodId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(name: "IX_BudgetRevisions_ResultScenarioId", table: "BudgetRevisions", column: "ResultScenarioId");
        migrationBuilder.CreateIndex(name: "IX_BudgetRevisions_SourceScenarioId", table: "BudgetRevisions", column: "SourceScenarioId");
        migrationBuilder.CreateIndex(
            name: "IX_BudgetRevisions_TenantId_RevisionNumber",
            table: "BudgetRevisions",
            columns: new[] { "TenantId", "RevisionNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_BudgetRevisions_TenantId_SourceScenarioId_Status",
            table: "BudgetRevisions",
            columns: new[] { "TenantId", "SourceScenarioId", "Status" });

        migrationBuilder.AddForeignKey(
            name: "FK_BudgetScenarios_BudgetScenarios_ParentScenarioId",
            table: "BudgetScenarios",
            column: "ParentScenarioId",
            principalTable: "BudgetScenarios",
            principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BudgetScenarios_BudgetScenarios_ParentScenarioId",
            table: "BudgetScenarios");

        migrationBuilder.DropTable(name: "BudgetRevisionLines");
        migrationBuilder.DropTable(name: "BudgetRevisions");
        migrationBuilder.DropIndex(name: "IX_BudgetScenarios_ParentScenarioId", table: "BudgetScenarios");
        migrationBuilder.DropColumn(name: "ParentScenarioId", table: "BudgetScenarios");
        migrationBuilder.DropColumn(name: "VersionNumber", table: "BudgetScenarios");
        migrationBuilder.DropColumn(name: "VersionType", table: "BudgetScenarios");
    }
}
