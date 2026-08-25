using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260825190000_AddFinanceDimensionRuleScope")]
public sealed class AddFinanceDimensionRuleScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId",
            table: "FinanceDimensionAccountRules");

        migrationBuilder.AddColumn<string>(
            name: "PostingAction",
            table: "FinanceDimensionAccountRules",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceDocumentType",
            table: "FinanceDimensionAccountRules",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceModule",
            table: "FinanceDimensionAccountRules",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId_SourceModule_SourceDocumentType_PostingAction",
            table: "FinanceDimensionAccountRules",
            columns: new[]
            {
                "TenantId", "AccountId", "FinanceDimensionDefinitionId",
                "SourceModule", "SourceDocumentType", "PostingAction"
            },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId_SourceModule_SourceDocumentType_PostingAction",
            table: "FinanceDimensionAccountRules");

        migrationBuilder.DropColumn(name: "PostingAction", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "SourceDocumentType", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "SourceModule", table: "FinanceDimensionAccountRules");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId",
            table: "FinanceDimensionAccountRules",
            columns: new[] { "TenantId", "AccountId", "FinanceDimensionDefinitionId" },
            unique: true);
    }
}
