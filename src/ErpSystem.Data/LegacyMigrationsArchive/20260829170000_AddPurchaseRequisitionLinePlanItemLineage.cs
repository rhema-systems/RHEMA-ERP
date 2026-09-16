using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260829170000_AddPurchaseRequisitionLinePlanItemLineage")]
public partial class AddPurchaseRequisitionLinePlanItemLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PurchaseRequisitionItems_TenantId",
            table: "PurchaseRequisitionItems");

        migrationBuilder.AddColumn<Guid>(
            name: "SourcePlanItemId",
            table: "PurchaseRequisitionItems",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE pri
            SET pri.[SourcePlanItemId] = pr.[SourcePlanItemId]
            FROM [dbo].[PurchaseRequisitionItems] AS pri
            INNER JOIN [dbo].[PurchaseRequisitions] AS pr
                ON pr.[Id] = pri.[RequisitionId]
                AND pr.[TenantId] = pri.[TenantId]
            WHERE pri.[SourcePlanItemId] IS NULL
              AND pr.[SourcePlanItemId] IS NOT NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseRequisitionItems_SourcePlanItemId",
            table: "PurchaseRequisitionItems",
            column: "SourcePlanItemId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseRequisitionItems_TenantId_SourcePlanItemId",
            table: "PurchaseRequisitionItems",
            columns: new[] { "TenantId", "SourcePlanItemId" });

        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseRequisitionItems_ProcurementPlanItems_SourcePlanItemId",
            table: "PurchaseRequisitionItems",
            column: "SourcePlanItemId",
            principalTable: "ProcurementPlanItems",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseRequisitionItems_ProcurementPlanItems_SourcePlanItemId",
            table: "PurchaseRequisitionItems");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseRequisitionItems_SourcePlanItemId",
            table: "PurchaseRequisitionItems");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseRequisitionItems_TenantId_SourcePlanItemId",
            table: "PurchaseRequisitionItems");

        migrationBuilder.DropColumn(
            name: "SourcePlanItemId",
            table: "PurchaseRequisitionItems");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseRequisitionItems_TenantId",
            table: "PurchaseRequisitionItems",
            column: "TenantId");
    }
}
