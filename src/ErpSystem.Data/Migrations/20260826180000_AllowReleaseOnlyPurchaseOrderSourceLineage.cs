using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Aligns the purchase-order source constraint with both supported procurement
/// routes. Direct awards retain approved-requisition and immutable-release
/// lineage; advanced awards additionally retain sourcing-case and readiness
/// lineage.
/// </summary>
public partial class AllowReleaseOnlyPurchaseOrderSourceLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseOrders_ApprovedSourceLineage",
            table: "PurchaseOrders");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseOrders_ApprovedSourceLineage",
            table: "PurchaseOrders",
            sql:
                """
                [ProcurementSourceType] BETWEEN 0 AND 5
                AND [ProcurementSourceId] IS NOT NULL
                AND LEN([ProcurementSourceReference]) BETWEEN 1 AND 100
                AND ISJSON([SourceSnapshotJson]) = 1
                AND LEN([SourceIntegrityHash]) = 64
                AND [SourceValidatedAtUtc] IS NOT NULL
                AND (
                    [ProcurementSourceType] = 5
                    OR (
                        [SourceRequisitionId] IS NOT NULL
                        AND [SourcingReleaseId] IS NOT NULL
                        AND (
                            (
                                [SourcingCaseId] IS NOT NULL
                                AND [AwardReadinessDecisionId] IS NOT NULL
                            )
                            OR (
                                [SourcingCaseId] IS NULL
                                AND [ProcurementSourceType] = 0
                                AND [AwardReadinessDecisionId] IS NULL
                            )
                            OR (
                                [SourcingCaseId] IS NULL
                                AND [ProcurementSourceType] IN (1, 2)
                                AND [AwardReadinessDecisionId] IS NOT NULL
                            )
                        )
                    )
                )
                """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseOrders_ApprovedSourceLineage",
            table: "PurchaseOrders");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseOrders_ApprovedSourceLineage",
            table: "PurchaseOrders",
            sql:
                """
                [ProcurementSourceType] BETWEEN 0 AND 5
                AND [ProcurementSourceId] IS NOT NULL
                AND LEN([ProcurementSourceReference]) BETWEEN 1 AND 100
                AND ISJSON([SourceSnapshotJson]) = 1
                AND LEN([SourceIntegrityHash]) = 64
                AND [SourceValidatedAtUtc] IS NOT NULL
                AND (
                    [ProcurementSourceType] = 5
                    OR (
                        [SourceRequisitionId] IS NOT NULL
                        AND [SourcingReleaseId] IS NOT NULL
                        AND [SourcingCaseId] IS NOT NULL
                        AND [AwardReadinessDecisionId] IS NOT NULL
                    )
                )
                """);
    }
}
