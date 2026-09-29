using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds durable statement-layout maker/checker evidence and removes the legacy singleton-role
/// index. Classification roles are semantic families; exact posting accounts are selected by the
/// owning module's governed configuration.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928110000_GovernLayoutApprovalAndClassificationRoles")]
public sealed class GovernLayoutApprovalAndClassificationRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccountClassifications_TenantId_AccountingBookId_SystemRole",
            table: "AccountClassifications");

        migrationBuilder.AddColumn<DateTime>(
            name: "SubmittedAt",
            table: "FinancialStatementLayoutVersions",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "SubmittedById",
            table: "FinancialStatementLayoutVersions",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "SubmittedByName",
            table: "FinancialStatementLayoutVersions",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "LastDecisionAt",
            table: "FinancialStatementLayoutVersions",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "LastDecisionById",
            table: "FinancialStatementLayoutVersions",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "LastDecisionByName",
            table: "FinancialStatementLayoutVersions",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "LastDecisionReason",
            table: "FinancialStatementLayoutVersions",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SubmittedAt", table: "FinancialStatementLayoutVersions");
        migrationBuilder.DropColumn(name: "SubmittedById", table: "FinancialStatementLayoutVersions");
        migrationBuilder.DropColumn(name: "SubmittedByName", table: "FinancialStatementLayoutVersions");
        migrationBuilder.DropColumn(name: "LastDecisionAt", table: "FinancialStatementLayoutVersions");
        migrationBuilder.DropColumn(name: "LastDecisionById", table: "FinancialStatementLayoutVersions");
        migrationBuilder.DropColumn(name: "LastDecisionByName", table: "FinancialStatementLayoutVersions");
        migrationBuilder.DropColumn(name: "LastDecisionReason", table: "FinancialStatementLayoutVersions");

        migrationBuilder.CreateIndex(
            name: "IX_AccountClassifications_TenantId_AccountingBookId_SystemRole",
            table: "AccountClassifications",
            columns: new[] { "TenantId", "AccountingBookId", "SystemRole" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [SystemRole] IS NOT NULL AND [SystemRole] <> 1 AND [SystemRole] <> 2");
    }
}
