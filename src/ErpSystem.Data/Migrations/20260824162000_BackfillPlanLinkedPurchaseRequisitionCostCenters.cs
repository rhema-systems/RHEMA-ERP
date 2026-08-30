using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class BackfillPlanLinkedPurchaseRequisitionCostCenters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE pr
               SET pr.CostCenter = LEFT(COALESCE(NULLIF(LTRIM(RTRIM(d.AccountCode)), ''), NULLIF(LTRIM(RTRIM(d.Code)), '')), 100),
                   pr.UpdatedAt = SYSUTCDATETIME(),
                   pr.UpdatedBy = 'Cost-centre lineage repair'
              FROM PurchaseRequisitions pr
              INNER JOIN ProcurementPlanItems pi
                      ON pi.Id = pr.SourcePlanItemId
                     AND pi.TenantId = pr.TenantId
                     AND pi.IsDeleted = 0
              INNER JOIN ProcurementPlans pp
                      ON pp.Id = pi.ProcurementPlanId
                     AND pp.TenantId = pr.TenantId
                     AND pp.IsDeleted = 0
              INNER JOIN Departments d
                      ON d.Id = pp.DepartmentId
                     AND d.TenantId = pr.TenantId
                     AND d.IsDeleted = 0
             WHERE pr.IsDeleted = 0
               AND pr.SourcePlanItemId IS NOT NULL
               AND (pr.CostCenter IS NULL OR LTRIM(RTRIM(pr.CostCenter)) = '')
               AND COALESCE(NULLIF(LTRIM(RTRIM(d.AccountCode)), ''), NULLIF(LTRIM(RTRIM(d.Code)), '')) IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data repair is intentionally not reversed.
    }
}
