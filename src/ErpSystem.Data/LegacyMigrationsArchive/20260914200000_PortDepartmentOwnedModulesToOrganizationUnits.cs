using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260914200000_PortDepartmentOwnedModulesToOrganizationUnits")]
public sealed class PortDepartmentOwnedModulesToOrganizationUnits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Retain the former Department references for historical records. New writes use
        // OrganizationUnitId, which is a node in HR's Structure → Level → Unit hierarchy.
        migrationBuilder.AlterColumn<Guid>(name: "DepartmentId", table: "ProcurementPlans", type: "uniqueidentifier", nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.AlterColumn<Guid>(name: "DepartmentId", table: "ProcurementBudgets", type: "uniqueidentifier", nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");

        AddOrganizationUnitColumn(migrationBuilder, "ProcurementPlans");
        AddOrganizationUnitColumn(migrationBuilder, "ProcurementBudgets");
        AddOrganizationUnitColumn(migrationBuilder, "ProcurementSchedules");
        AddOrganizationUnitColumn(migrationBuilder, "PurchaseRequisitions");
        AddOrganizationUnitColumn(migrationBuilder, "InventoryRequisitions");
        AddOrganizationUnitColumn(migrationBuilder, "InventoryIssueVouchers");
        AddOrganizationUnitColumn(migrationBuilder, "InventoryAllocations");

        migrationBuilder.CreateIndex(name: "IX_ProcurementPlans_OrganizationUnitId", table: "ProcurementPlans", column: "OrganizationUnitId");
        migrationBuilder.CreateIndex(name: "IX_ProcurementBudgets_OrganizationUnitId", table: "ProcurementBudgets", column: "OrganizationUnitId");
        migrationBuilder.CreateIndex(name: "IX_ProcurementSchedules_OrganizationUnitId", table: "ProcurementSchedules", column: "OrganizationUnitId");
        migrationBuilder.CreateIndex(name: "IX_PurchaseRequisitions_TenantId_OrganizationUnitId", table: "PurchaseRequisitions", columns: new[] { "TenantId", "OrganizationUnitId" });
        migrationBuilder.CreateIndex(name: "IX_InventoryRequisitions_OrganizationUnitId", table: "InventoryRequisitions", column: "OrganizationUnitId");
        migrationBuilder.CreateIndex(name: "IX_InventoryIssueVouchers_TenantId_OrganizationUnitId", table: "InventoryIssueVouchers", columns: new[] { "TenantId", "OrganizationUnitId" });
        migrationBuilder.CreateIndex(name: "IX_InventoryAllocations_TenantId_OrganizationUnitId_Status_ExpirationDate", table: "InventoryAllocations", columns: new[] { "TenantId", "OrganizationUnitId", "Status", "ExpirationDate" });

        migrationBuilder.DropCheckConstraint(name: "CK_InventoryAllocations_ProjectLineage", table: "InventoryAllocations");
        migrationBuilder.AddCheckConstraint(
            name: "CK_InventoryAllocations_ProjectLineage",
            table: "InventoryAllocations",
            sql: "[AllocationType] <> 'ProjectRequisition' OR ([InventoryRequisitionId] IS NOT NULL AND [InventoryRequisitionItemId] IS NOT NULL AND [ProjectId] IS NOT NULL AND [OrganizationUnitId] IS NOT NULL AND [LocationId] IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_InventoryAllocations_ProjectLineage", table: "InventoryAllocations");
        migrationBuilder.AddCheckConstraint(
            name: "CK_InventoryAllocations_ProjectLineage",
            table: "InventoryAllocations",
            sql: "[AllocationType] <> 'ProjectRequisition' OR ([InventoryRequisitionId] IS NOT NULL AND [InventoryRequisitionItemId] IS NOT NULL AND [ProjectId] IS NOT NULL AND [DepartmentId] IS NOT NULL AND [LocationId] IS NOT NULL)");

        migrationBuilder.DropIndex(name: "IX_InventoryAllocations_TenantId_OrganizationUnitId_Status_ExpirationDate", table: "InventoryAllocations");
        migrationBuilder.DropIndex(name: "IX_InventoryIssueVouchers_TenantId_OrganizationUnitId", table: "InventoryIssueVouchers");
        migrationBuilder.DropIndex(name: "IX_InventoryRequisitions_OrganizationUnitId", table: "InventoryRequisitions");
        migrationBuilder.DropIndex(name: "IX_PurchaseRequisitions_TenantId_OrganizationUnitId", table: "PurchaseRequisitions");
        migrationBuilder.DropIndex(name: "IX_ProcurementSchedules_OrganizationUnitId", table: "ProcurementSchedules");
        migrationBuilder.DropIndex(name: "IX_ProcurementBudgets_OrganizationUnitId", table: "ProcurementBudgets");
        migrationBuilder.DropIndex(name: "IX_ProcurementPlans_OrganizationUnitId", table: "ProcurementPlans");

        DropOrganizationUnitColumn(migrationBuilder, "InventoryAllocations");
        DropOrganizationUnitColumn(migrationBuilder, "InventoryIssueVouchers");
        DropOrganizationUnitColumn(migrationBuilder, "InventoryRequisitions");
        DropOrganizationUnitColumn(migrationBuilder, "PurchaseRequisitions");
        DropOrganizationUnitColumn(migrationBuilder, "ProcurementSchedules");
        DropOrganizationUnitColumn(migrationBuilder, "ProcurementBudgets");
        DropOrganizationUnitColumn(migrationBuilder, "ProcurementPlans");

        migrationBuilder.AlterColumn<Guid>(name: "DepartmentId", table: "ProcurementBudgets", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(name: "DepartmentId", table: "ProcurementPlans", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
    }

    private static void AddOrganizationUnitColumn(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<Guid>(name: "OrganizationUnitId", table: table, type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddForeignKey(
            name: $"FK_{table}_OrganizationUnits_OrganizationUnitId",
            table: table,
            column: "OrganizationUnitId",
            principalTable: "OrganizationUnits",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    private static void DropOrganizationUnitColumn(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropForeignKey(name: $"FK_{table}_OrganizationUnits_OrganizationUnitId", table: table);
        migrationBuilder.DropColumn(name: "OrganizationUnitId", table: table);
    }
}
