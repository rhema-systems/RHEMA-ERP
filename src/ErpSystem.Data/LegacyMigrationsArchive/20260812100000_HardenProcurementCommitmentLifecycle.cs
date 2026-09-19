using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260812100000_HardenProcurementCommitmentLifecycle")]
public partial class HardenProcurementCommitmentLifecycle : Migration
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

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBudgetCommitments_DownstreamExposure]
            ON [dbo].[ProcurementBudgetCommitments]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status = 1
                      AND i.Status = 2
                      AND (
                           EXISTS (
                               SELECT 1
                               FROM dbo.PurchaseOrders po
                               WHERE po.TenantId = i.TenantId
                                 AND po.SourceRequisitionId = i.PurchaseRequisitionId
                                 AND po.IsDeleted = 0
                                 AND po.Status NOT IN ('Cancelled', 'Rejected'))
                        OR EXISTS (
                               SELECT 1
                               FROM dbo.Contracts contract
                               JOIN dbo.Tenders tender
                                 ON tender.Id = contract.TenderId
                                AND tender.TenantId = contract.TenantId
                                AND tender.IsDeleted = 0
                               WHERE contract.TenantId = i.TenantId
                                 AND tender.SourcePurchaseRequisitionId = i.PurchaseRequisitionId
                                 AND contract.IsDeleted = 0
                                 AND contract.Status <> 'Terminated')
                      ))
                    THROW 52042, 'A budget commitment with active purchase-order or contract exposure cannot be released.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitments_DownstreamExposure];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrders_GovernedCommitment];");
    }
}
