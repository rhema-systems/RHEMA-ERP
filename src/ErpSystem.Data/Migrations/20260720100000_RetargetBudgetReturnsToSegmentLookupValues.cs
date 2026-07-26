using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260720100000_RetargetBudgetReturnsToSegmentLookupValues")]
public partial class RetargetBudgetReturnsToSegmentLookupValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[BudgetReturns]', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'[dbo].[FK_BudgetReturns_AccountSegmentValues_SegmentValueId]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[BudgetReturns] DROP CONSTRAINT [FK_BudgetReturns_AccountSegmentValues_SegmentValueId];

                UPDATE budgetReturn
                SET [SegmentValueId] = accountSegmentValue.[SegmentLookupValueId]
                FROM [dbo].[BudgetReturns] budgetReturn
                INNER JOIN [dbo].[AccountSegmentValues] accountSegmentValue
                    ON accountSegmentValue.[Id] = budgetReturn.[SegmentValueId]
                WHERE budgetReturn.[SegmentValueId] IS NOT NULL
                    AND accountSegmentValue.[SegmentLookupValueId] IS NOT NULL;

                UPDATE budgetReturn
                SET [SegmentValueId] = NULL
                FROM [dbo].[BudgetReturns] budgetReturn
                LEFT JOIN [dbo].[SegmentLookupValues] lookupValue
                    ON lookupValue.[Id] = budgetReturn.[SegmentValueId]
                WHERE budgetReturn.[SegmentValueId] IS NOT NULL
                    AND lookupValue.[Id] IS NULL;
            END
            """);

        migrationBuilder.AddForeignKey(
            name: "FK_BudgetReturns_SegmentLookupValues_SegmentValueId",
            table: "BudgetReturns",
            column: "SegmentValueId",
            principalTable: "SegmentLookupValues",
            principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BudgetReturns_SegmentLookupValues_SegmentValueId",
            table: "BudgetReturns");

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[BudgetReturns]', N'U') IS NOT NULL
            BEGIN
                UPDATE budgetReturn
                SET [SegmentValueId] = accountSegmentValue.[Id]
                FROM [dbo].[BudgetReturns] budgetReturn
                OUTER APPLY
                (
                    SELECT TOP (1) segmentValue.[Id]
                    FROM [dbo].[AccountSegmentValues] segmentValue
                    WHERE segmentValue.[TenantId] = budgetReturn.[TenantId]
                        AND segmentValue.[SegmentLookupValueId] = budgetReturn.[SegmentValueId]
                        AND segmentValue.[IsDeleted] = 0
                    ORDER BY segmentValue.[CreatedAt]
                ) accountSegmentValue
                WHERE budgetReturn.[SegmentValueId] IS NOT NULL
                    AND accountSegmentValue.[Id] IS NOT NULL;

                UPDATE budgetReturn
                SET [SegmentValueId] = NULL
                FROM [dbo].[BudgetReturns] budgetReturn
                LEFT JOIN [dbo].[AccountSegmentValues] accountSegmentValue
                    ON accountSegmentValue.[Id] = budgetReturn.[SegmentValueId]
                WHERE budgetReturn.[SegmentValueId] IS NOT NULL
                    AND accountSegmentValue.[Id] IS NULL;
            END
            """);

        migrationBuilder.AddForeignKey(
            name: "FK_BudgetReturns_AccountSegmentValues_SegmentValueId",
            table: "BudgetReturns",
            column: "SegmentValueId",
            principalTable: "AccountSegmentValues",
            principalColumn: "Id");
    }
}
