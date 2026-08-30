using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Aligns the SQL purchase-order guard with the controlled PO lifecycle. Draft
/// creation does not post Finance exposure. Submission creates the commitment
/// atomically before the PO enters a governed status. A sourcing release may
/// retain a commitment snapshot when one already existed, while the approved
/// requisition remains the authoritative key for a later commitment.
/// </summary>
public partial class AlignPurchaseOrderCommitmentWithRequisition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrders_GovernedCommitment]
            ON [dbo].[PurchaseOrders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN dbo.ProcurementRequisitionSourcingReleases release
                      ON release.Id = i.SourcingReleaseId
                     AND release.TenantId = i.TenantId
                     AND release.PurchaseRequisitionId = i.SourceRequisitionId
                     AND release.IsDeleted = 0
                    LEFT JOIN dbo.PurchaseRequisitions requisition
                      ON requisition.Id = i.SourceRequisitionId
                     AND requisition.TenantId = i.TenantId
                     AND requisition.IsDeleted = 0
                    LEFT JOIN dbo.ProcurementBudgetCommitments commitment
                      ON commitment.TenantId = i.TenantId
                     AND commitment.PurchaseRequisitionId = i.SourceRequisitionId
                     AND commitment.IsDeleted = 0
                     AND (
                          release.BudgetCommitmentId IS NULL
                          OR commitment.Id = release.BudgetCommitmentId
                     )
                    LEFT JOIN dbo.ProcurementBudgets budget
                      ON budget.Id = commitment.ProcurementBudgetId
                     AND budget.TenantId = i.TenantId
                     AND budget.IsDeleted = 0
                    WHERE i.ProcurementSourceType <> 5
                      AND i.Status IN ('Approved', 'Sent', 'Acknowledged')
                      AND (
                           release.Id IS NULL
                        OR requisition.Id IS NULL
                        OR commitment.Id IS NULL
                        OR budget.Id IS NULL
                        OR (
                             release.BudgetCommitmentReference IS NOT NULL
                             AND release.BudgetCommitmentReference <>
                                 commitment.ReservationReference
                           )
                        OR requisition.BudgetId IS NULL
                        OR requisition.BudgetId <> commitment.ProcurementBudgetId
                        OR commitment.Status <> 1
                        OR commitment.ReservedAmount <= 0
                        OR UPPER(LTRIM(RTRIM(commitment.Currency))) <>
                           UPPER(LTRIM(RTRIM(requisition.Currency)))
                        OR UPPER(LTRIM(RTRIM(commitment.Currency))) <>
                           UPPER(LTRIM(RTRIM(i.Currency)))
                        OR UPPER(LTRIM(RTRIM(commitment.Currency))) <>
                           UPPER(LTRIM(RTRIM(budget.Currency)))
                        OR budget.Status NOT IN ('Approved', 'Active')
                        OR budget.ApprovedById IS NULL
                        OR budget.ApprovedDate IS NULL
                        OR (budget.EffectiveDate IS NOT NULL AND
                            budget.EffectiveDate > SYSUTCDATETIME())
                        OR (budget.ExpiryDate IS NOT NULL AND
                            budget.ExpiryDate < SYSUTCDATETIME())
                        OR budget.CommittedAmount < commitment.ReservedAmount
                        OR (
                            SELECT COALESCE(SUM(po.TotalAmount), 0)
                            FROM dbo.PurchaseOrders po
                            WHERE po.TenantId = i.TenantId
                              AND po.SourceRequisitionId = i.SourceRequisitionId
                              AND po.IsDeleted = 0
                              AND po.Status NOT IN ('Cancelled', 'Rejected')
                           ) > commitment.ReservedAmount
                      ))
                    THROW 52041, 'Purchase-order issuance requires the exact active tenant budget commitment with sufficient reserved exposure.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrders_GovernedCommitment]
            ON [dbo].[PurchaseOrders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN dbo.ProcurementRequisitionSourcingReleases release
                      ON release.Id = i.SourcingReleaseId
                     AND release.TenantId = i.TenantId
                     AND release.PurchaseRequisitionId = i.SourceRequisitionId
                     AND release.IsDeleted = 0
                    LEFT JOIN dbo.PurchaseRequisitions requisition
                      ON requisition.Id = i.SourceRequisitionId
                     AND requisition.TenantId = i.TenantId
                     AND requisition.IsDeleted = 0
                    LEFT JOIN dbo.ProcurementBudgetCommitments commitment
                      ON commitment.Id = release.BudgetCommitmentId
                     AND commitment.TenantId = i.TenantId
                     AND commitment.PurchaseRequisitionId = i.SourceRequisitionId
                     AND commitment.IsDeleted = 0
                    LEFT JOIN dbo.ProcurementBudgets budget
                      ON budget.Id = commitment.ProcurementBudgetId
                     AND budget.TenantId = i.TenantId
                     AND budget.IsDeleted = 0
                    WHERE i.ProcurementSourceType <> 5
                      AND (
                           d.Id IS NULL
                        OR (
                             ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                             AND i.Status IN (
                                 'Submitted', 'Pending Approval',
                                 'Approved', 'Sent', 'Acknowledged')
                           )
                      )
                      AND (
                           release.Id IS NULL
                        OR requisition.Id IS NULL
                        OR commitment.Id IS NULL
                        OR budget.Id IS NULL
                        OR release.BudgetCommitmentReference <> commitment.ReservationReference
                        OR requisition.BudgetId IS NULL
                        OR requisition.BudgetId <> commitment.ProcurementBudgetId
                        OR commitment.Status <> 1
                        OR commitment.ReservedAmount <= 0
                        OR UPPER(LTRIM(RTRIM(commitment.Currency))) <>
                           UPPER(LTRIM(RTRIM(requisition.Currency)))
                        OR UPPER(LTRIM(RTRIM(commitment.Currency))) <>
                           UPPER(LTRIM(RTRIM(i.Currency)))
                        OR UPPER(LTRIM(RTRIM(commitment.Currency))) <>
                           UPPER(LTRIM(RTRIM(budget.Currency)))
                        OR budget.Status NOT IN ('Approved', 'Active')
                        OR budget.ApprovedById IS NULL
                        OR budget.ApprovedDate IS NULL
                        OR (budget.EffectiveDate IS NOT NULL AND
                            budget.EffectiveDate > SYSUTCDATETIME())
                        OR (budget.ExpiryDate IS NOT NULL AND
                            budget.ExpiryDate < SYSUTCDATETIME())
                        OR budget.CommittedAmount < commitment.ReservedAmount
                        OR (
                            SELECT COALESCE(SUM(po.TotalAmount), 0)
                            FROM dbo.PurchaseOrders po
                            WHERE po.TenantId = i.TenantId
                              AND po.SourceRequisitionId = i.SourceRequisitionId
                              AND po.IsDeleted = 0
                              AND po.Status NOT IN ('Cancelled', 'Rejected')
                           ) > commitment.ReservedAmount
                      ))
                    THROW 52041, 'Purchase-order issuance requires the exact active tenant budget commitment with sufficient reserved exposure.', 1;
            END;
            """);
    }
}
