using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801123500_AddBudgetCommitmentConsumptionTimestamp")]
public sealed class AddBudgetCommitmentConsumptionTimestamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ConsumedAtUtc",
            table: "ProcurementBudgetCommitments",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[ProcurementBudgetCommitments]
            SET [ConsumedAtUtc] = COALESCE([UpdatedAt], [ReservedAtUtc], [CreatedAt])
            WHERE [Status] = 3 AND [ConsumedAtUtc] IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ConsumedAtUtc",
            table: "ProcurementBudgetCommitments");
    }
}
