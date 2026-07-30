using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260729210000_TDC0403FrameworkLineageFailClosed")]
public partial class TDC0403FrameworkLineageFailClosed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CreateTrigger(failClosed: true));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CreateTrigger(failClosed: false));
    }

    private static string CreateTrigger(bool failClosed) =>
        failClosed
            ? """
              CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkCallOffs_PurchaseOrderSource]
              ON [ProcurementFrameworkCallOffs]
              AFTER INSERT, UPDATE
              AS
              BEGIN
                  SET NOCOUNT ON;

                  IF EXISTS (
                      SELECT 1
                      FROM inserted callOff
                      LEFT JOIN PurchaseOrders purchaseOrder
                        ON purchaseOrder.Id = callOff.PurchaseOrderId
                       AND purchaseOrder.TenantId = callOff.TenantId
                      LEFT JOIN ProcurementFrameworkAgreements agreement
                        ON agreement.Id = callOff.AgreementId
                       AND agreement.TenantId = callOff.TenantId
                       AND agreement.IsDeleted = 0
                      LEFT JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = agreement.AwardReadinessDecisionId
                       AND readiness.TenantId = callOff.TenantId
                       AND readiness.Status = 1
                       AND readiness.IsDeleted = 0
                      LEFT JOIN ProcurementSourcingCases sourcing
                        ON sourcing.Id = purchaseOrder.SourcingCaseId
                       AND sourcing.TenantId = callOff.TenantId
                       AND sourcing.SourcingReleaseId =
                           purchaseOrder.SourcingReleaseId
                       AND sourcing.Status <> 3
                       AND sourcing.IsDeleted = 0
                      LEFT JOIN RequestForQuotations rfq
                        ON readiness.SourceType = 0
                       AND rfq.Id = readiness.SourceId
                       AND rfq.TenantId = callOff.TenantId
                       AND rfq.SourcingCaseId = sourcing.Id
                       AND rfq.SourcingReleaseId = sourcing.SourcingReleaseId
                       AND rfq.IsDeleted = 0
                      LEFT JOIN Tenders tender
                        ON readiness.SourceType = 1
                       AND tender.Id = readiness.SourceId
                       AND tender.TenantId = callOff.TenantId
                       AND tender.SourcingCaseId = sourcing.Id
                       AND tender.SourcingReleaseId = sourcing.SourcingReleaseId
                       AND tender.IsDeleted = 0
                      LEFT JOIN ProcurementExceptionalSourcingControls exceptional
                        ON readiness.SourceType = 2
                       AND exceptional.Id = readiness.SourceId
                       AND exceptional.TenantId = callOff.TenantId
                       AND exceptional.SourcingCaseId = sourcing.Id
                       AND exceptional.IsDeleted = 0
                      WHERE purchaseOrder.Id IS NULL
                         OR agreement.Id IS NULL
                         OR readiness.Id IS NULL
                         OR sourcing.Id IS NULL
                         OR purchaseOrder.ProcurementSourceType <> 4
                         OR purchaseOrder.ProcurementSourceId <> callOff.Id
                         OR purchaseOrder.ProcurementSourceReference <>
                              callOff.CallOffNumber
                         OR purchaseOrder.SourceRequisitionId <>
                              callOff.SourceRequisitionId
                         OR purchaseOrder.BusinessPartnerId <>
                              callOff.BusinessPartnerId
                         OR purchaseOrder.AwardReadinessDecisionId <>
                              agreement.AwardReadinessDecisionId
                         OR (
                              readiness.SourceType = 0 AND rfq.Id IS NULL
                         )
                         OR (
                              readiness.SourceType = 1 AND tender.Id IS NULL
                         )
                         OR (
                              readiness.SourceType = 2 AND exceptional.Id IS NULL
                         )
                         OR LEN(ISNULL(purchaseOrder.SourceIntegrityHash, '')) <> 64)
                      THROW 51212, 'The framework call-off purchase order does not carry the exact governed source lineage.', 1;
              END
              """
            : """
              CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkCallOffs_PurchaseOrderSource]
              ON [ProcurementFrameworkCallOffs]
              AFTER INSERT, UPDATE
              AS
              BEGIN
                  SET NOCOUNT ON;

                  IF EXISTS (
                      SELECT 1
                      FROM inserted callOff
                      JOIN PurchaseOrders purchaseOrder
                        ON purchaseOrder.Id = callOff.PurchaseOrderId
                       AND purchaseOrder.TenantId = callOff.TenantId
                      JOIN ProcurementFrameworkAgreements agreement
                        ON agreement.Id = callOff.AgreementId
                       AND agreement.TenantId = callOff.TenantId
                      JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = agreement.AwardReadinessDecisionId
                       AND readiness.TenantId = callOff.TenantId
                       AND readiness.Status = 1
                       AND readiness.IsDeleted = 0
                      JOIN ProcurementSourcingCases sourcing
                        ON sourcing.Id = purchaseOrder.SourcingCaseId
                       AND sourcing.TenantId = callOff.TenantId
                       AND sourcing.SourcingReleaseId =
                           purchaseOrder.SourcingReleaseId
                       AND sourcing.Status <> 3
                       AND sourcing.IsDeleted = 0
                      LEFT JOIN RequestForQuotations rfq
                        ON readiness.SourceType = 0
                       AND rfq.Id = readiness.SourceId
                       AND rfq.TenantId = callOff.TenantId
                       AND rfq.SourcingCaseId = sourcing.Id
                       AND rfq.SourcingReleaseId = sourcing.SourcingReleaseId
                       AND rfq.IsDeleted = 0
                      LEFT JOIN Tenders tender
                        ON readiness.SourceType = 1
                       AND tender.Id = readiness.SourceId
                       AND tender.TenantId = callOff.TenantId
                       AND tender.SourcingCaseId = sourcing.Id
                       AND tender.SourcingReleaseId = sourcing.SourcingReleaseId
                       AND tender.IsDeleted = 0
                      LEFT JOIN ProcurementExceptionalSourcingControls exceptional
                        ON readiness.SourceType = 2
                       AND exceptional.Id = readiness.SourceId
                       AND exceptional.TenantId = callOff.TenantId
                       AND exceptional.SourcingCaseId = sourcing.Id
                       AND exceptional.IsDeleted = 0
                      WHERE purchaseOrder.ProcurementSourceType <> 4
                         OR purchaseOrder.ProcurementSourceId <> callOff.Id
                         OR purchaseOrder.ProcurementSourceReference <>
                              callOff.CallOffNumber
                         OR purchaseOrder.SourceRequisitionId <>
                              callOff.SourceRequisitionId
                         OR purchaseOrder.BusinessPartnerId <>
                              callOff.BusinessPartnerId
                         OR purchaseOrder.AwardReadinessDecisionId <>
                              agreement.AwardReadinessDecisionId
                         OR (
                              readiness.SourceType = 0 AND rfq.Id IS NULL
                         )
                         OR (
                              readiness.SourceType = 1 AND tender.Id IS NULL
                         )
                         OR (
                              readiness.SourceType = 2 AND exceptional.Id IS NULL
                         )
                         OR LEN(ISNULL(purchaseOrder.SourceIntegrityHash, '')) <> 64)
                      THROW 51212, 'The framework call-off purchase order does not carry the exact governed source lineage.', 1;
              END
              """;
}
