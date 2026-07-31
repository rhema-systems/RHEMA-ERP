using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260729153000_TDC0403MandatoryPurchaseOrderSources")]
public partial class TDC0403MandatoryPurchaseOrderSources : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AwardReadinessDecisionId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProcurementSourceId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProcurementSourceReference",
            table: "PurchaseOrders",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ProcurementSourceType",
            table: "PurchaseOrders",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceIntegrityHash",
            table: "PurchaseOrders",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceSnapshotJson",
            table: "PurchaseOrders",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "SourceValidatedAtUtc",
            table: "PurchaseOrders",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourcingCaseId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourcingReleaseId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [PurchaseOrders]
            SET [ProcurementSourceType] = 5,
                [ProcurementSourceId] = [Id],
                [ProcurementSourceReference] =
                    LEFT(CONCAT('LEGACY/', [OrderNumber]), 100),
                [SourceSnapshotJson] =
                    CONCAT('{"schema":"TDC-0403","sourceType":"HistoricalMigration","purchaseOrderId":"',
                           CONVERT(varchar(36), [Id]), '"}'),
                [SourceIntegrityHash] =
                    LOWER(CONVERT(varchar(64),
                        HASHBYTES('SHA2_256',
                            CONCAT('TDC-0403|HistoricalMigration|',
                                   CONVERT(varchar(36), [Id]), '|', [OrderNumber])),
                        2)),
                [SourceValidatedAtUtc] = [CreatedAt]
            WHERE [ProcurementSourceType] IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_AwardReadinessDecisionId",
            table: "PurchaseOrders",
            column: "AwardReadinessDecisionId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_SourcingCaseId",
            table: "PurchaseOrders",
            column: "SourcingCaseId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_SourcingReleaseId",
            table: "PurchaseOrders",
            column: "SourcingReleaseId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_TenantId_ProcurementSourceType_ProcurementSourceId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "ProcurementSourceType", "ProcurementSourceId" });

        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseOrders_ProcurementAwardReadinessDecisions_AwardReadinessDecisionId",
            table: "PurchaseOrders",
            column: "AwardReadinessDecisionId",
            principalTable: "ProcurementAwardReadinessDecisions",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseOrders_ProcurementSourcingCases_SourcingCaseId",
            table: "PurchaseOrders",
            column: "SourcingCaseId",
            principalTable: "ProcurementSourcingCases",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseOrders_ProcurementRequisitionSourcingReleases_SourcingReleaseId",
            table: "PurchaseOrders",
            column: "SourcingReleaseId",
            principalTable: "ProcurementRequisitionSourcingReleases",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

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

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_PurchaseOrders_ApprovedSourceProtected]
            ON [PurchaseOrders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE d.Id IS NULL
                      AND i.ProcurementSourceType = 5)
                    THROW 51201, 'HistoricalMigration cannot authorize a new purchase order.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    WHERE i.ProcurementSourceType IS NULL
                       OR i.ProcurementSourceId IS NULL
                       OR NULLIF(LTRIM(RTRIM(i.ProcurementSourceReference)), '') IS NULL
                       OR i.SourceSnapshotJson IS NULL
                       OR ISJSON(i.SourceSnapshotJson) <> 1
                       OR LEN(ISNULL(i.SourceIntegrityHash, '')) <> 64
                       OR i.SourceValidatedAtUtc IS NULL
                       OR (i.ProcurementSourceType <> 5 AND (
                              i.SourceRequisitionId IS NULL
                           OR i.SourcingReleaseId IS NULL
                           OR i.SourcingCaseId IS NULL
                           OR i.AwardReadinessDecisionId IS NULL)))
                    THROW 51202, 'A complete immutable approved source lineage is required for every purchase order.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE ISNULL(i.ProcurementSourceType, -1)
                            <> ISNULL(d.ProcurementSourceType, -1)
                       OR ISNULL(i.ProcurementSourceId,
                                 '00000000-0000-0000-0000-000000000000')
                            <> ISNULL(d.ProcurementSourceId,
                                 '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.ProcurementSourceReference, '')
                            <> ISNULL(d.ProcurementSourceReference, '')
                       OR ISNULL(i.SourceRequisitionId,
                                 '00000000-0000-0000-0000-000000000000')
                            <> ISNULL(d.SourceRequisitionId,
                                 '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.SourcingReleaseId,
                                 '00000000-0000-0000-0000-000000000000')
                            <> ISNULL(d.SourcingReleaseId,
                                 '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.SourcingCaseId,
                                 '00000000-0000-0000-0000-000000000000')
                            <> ISNULL(d.SourcingCaseId,
                                 '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.AwardReadinessDecisionId,
                                 '00000000-0000-0000-0000-000000000000')
                            <> ISNULL(d.AwardReadinessDecisionId,
                                 '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.SourceSnapshotJson, '')
                            <> ISNULL(d.SourceSnapshotJson, '')
                       OR ISNULL(i.SourceIntegrityHash, '')
                            <> ISNULL(d.SourceIntegrityHash, ''))
                    THROW 51203, 'Purchase-order approved source lineage is immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND i.ProcurementSourceType = 5)
                    THROW 51204, 'A historical purchase order cannot enter a new approval or issue lifecycle without governed source remediation.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN PurchaseRequisitions pr
                        ON pr.Id = i.SourceRequisitionId
                       AND pr.TenantId = i.TenantId
                       AND pr.IsDeleted = 0
                    LEFT JOIN ProcurementRequisitionSourcingReleases release
                        ON release.Id = i.SourcingReleaseId
                       AND release.TenantId = i.TenantId
                       AND release.PurchaseRequisitionId = i.SourceRequisitionId
                       AND release.IsDeleted = 0
                    LEFT JOIN ProcurementSourcingCases sourcing
                        ON sourcing.Id = i.SourcingCaseId
                       AND sourcing.TenantId = i.TenantId
                       AND sourcing.PurchaseRequisitionId = i.SourceRequisitionId
                       AND sourcing.SourcingReleaseId = i.SourcingReleaseId
                       AND sourcing.IsDeleted = 0
                    LEFT JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = i.AwardReadinessDecisionId
                       AND readiness.TenantId = i.TenantId
                       AND readiness.Status = 1
                       AND readiness.IsDeleted = 0
                    WHERE i.ProcurementSourceType NOT IN (4, 5)
                      AND (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND (
                           pr.Id IS NULL
                        OR pr.Status NOT IN ('Approved', 'Ordered')
                        OR release.Id IS NULL
                        OR sourcing.Id IS NULL
                        OR sourcing.Status = 3
                        OR readiness.Id IS NULL))
                    THROW 51205, 'The approved requisition, sourcing release, sourcing case, or award-readiness decision is invalid.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE i.ProcurementSourceType NOT IN (4, 5)
                      AND (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND EXISTS (
                          SELECT 1
                          FROM ProcurementAwardReadinessDecisions newer
                          JOIN ProcurementAwardReadinessDecisions currentDecision
                            ON currentDecision.Id = i.AwardReadinessDecisionId
                           AND currentDecision.TenantId = i.TenantId
                          WHERE newer.TenantId = i.TenantId
                            AND newer.SourceType = currentDecision.SourceType
                            AND newer.SourceId = currentDecision.SourceId
                            AND newer.IsDeleted = 0
                            AND newer.DecisionSequence >
                                currentDecision.DecisionSequence))
                    THROW 51206, 'The purchase order does not reference the latest award-readiness decision.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN RequestForQuotations rfq
                        ON rfq.Id = i.ProcurementSourceId
                       AND rfq.TenantId = i.TenantId
                       AND rfq.IsDeleted = 0
                    LEFT JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = i.AwardReadinessDecisionId
                       AND readiness.TenantId = i.TenantId
                    WHERE i.ProcurementSourceType = 0
                      AND (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND (
                           rfq.Id IS NULL
                        OR rfq.Status NOT IN ('Approved', 'Awarded')
                        OR rfq.SourcePurchaseRequisitionId <> i.SourceRequisitionId
                        OR rfq.SourcingReleaseId <> i.SourcingReleaseId
                        OR rfq.SourcingCaseId <> i.SourcingCaseId
                        OR readiness.SourceType <> 0
                        OR readiness.SourceId <> rfq.Id
                        OR NOT EXISTS (
                            SELECT 1
                            FROM RequestForQuotationAwardLines awardLine
                            WHERE awardLine.TenantId = i.TenantId
                              AND awardLine.RfqId = rfq.Id
                              AND awardLine.BusinessPartnerId = i.BusinessPartnerId
                              AND awardLine.IsDeleted = 0)))
                    THROW 51207, 'The RFQ award source no longer authorizes this purchase order.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN TenderAwards award
                        ON award.Id = i.ProcurementSourceId
                       AND award.TenantId = i.TenantId
                       AND award.IsDeleted = 0
                    LEFT JOIN Tenders tender
                        ON tender.Id = award.TenderId
                       AND tender.TenantId = i.TenantId
                       AND tender.IsDeleted = 0
                    LEFT JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = i.AwardReadinessDecisionId
                       AND readiness.TenantId = i.TenantId
                    WHERE i.ProcurementSourceType = 1
                      AND (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND (
                           award.Id IS NULL
                        OR award.Status NOT IN ('Awarded', 'ContractSigned')
                        OR award.BusinessPartnerId <> i.BusinessPartnerId
                        OR tender.SourcePurchaseRequisitionId <> i.SourceRequisitionId
                        OR tender.SourcingReleaseId <> i.SourcingReleaseId
                        OR tender.SourcingCaseId <> i.SourcingCaseId
                        OR readiness.SourceType <> 1
                        OR readiness.SourceId <> tender.Id))
                    THROW 51208, 'The tender award source no longer authorizes this purchase order.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN Contracts contract
                        ON contract.Id = i.ProcurementSourceId
                       AND contract.TenantId = i.TenantId
                       AND contract.IsDeleted = 0
                    LEFT JOIN TenderAwards award
                        ON award.Id = contract.TenderAwardId
                       AND award.TenantId = i.TenantId
                       AND award.IsDeleted = 0
                    LEFT JOIN Tenders tender
                        ON tender.Id = contract.TenderId
                       AND tender.TenantId = i.TenantId
                       AND tender.IsDeleted = 0
                    LEFT JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = i.AwardReadinessDecisionId
                       AND readiness.TenantId = i.TenantId
                    WHERE i.ProcurementSourceType = 2
                      AND (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND (
                           contract.Id IS NULL
                        OR contract.Status <> 'Active'
                        OR (contract.StartDate IS NOT NULL
                            AND contract.StartDate > SYSUTCDATETIME())
                        OR (contract.EndDate IS NOT NULL
                            AND contract.EndDate < SYSUTCDATETIME())
                        OR contract.BusinessPartnerId <> i.BusinessPartnerId
                        OR award.Id IS NULL
                        OR tender.SourcePurchaseRequisitionId <> i.SourceRequisitionId
                        OR tender.SourcingReleaseId <> i.SourcingReleaseId
                        OR tender.SourcingCaseId <> i.SourcingCaseId
                        OR readiness.SourceType <> 1
                        OR readiness.SourceId <> tender.Id))
                    THROW 51209, 'The active contract source no longer authorizes this purchase order.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN ProcurementExceptionalSourcingControls exceptional
                        ON exceptional.Id = i.ProcurementSourceId
                       AND exceptional.TenantId = i.TenantId
                       AND exceptional.IsDeleted = 0
                    LEFT JOIN TenderBids bid
                        ON bid.Id = exceptional.AwardBidId
                       AND bid.TenantId = i.TenantId
                       AND bid.IsDeleted = 0
                    LEFT JOIN Tenders tender
                        ON tender.Id = exceptional.TenderId
                       AND tender.TenantId = i.TenantId
                       AND tender.IsDeleted = 0
                    LEFT JOIN ProcurementAwardReadinessDecisions readiness
                        ON readiness.Id = i.AwardReadinessDecisionId
                       AND readiness.TenantId = i.TenantId
                    WHERE i.ProcurementSourceType = 3
                      AND (d.Id IS NULL OR (
                               ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                               AND i.Status IN (
                                   'Submitted', 'Pending Approval',
                                   'Approved', 'Sent', 'Acknowledged')))
                      AND (
                           exceptional.Id IS NULL
                        OR exceptional.Status NOT BETWEEN 5 AND 8
                        OR exceptional.SourcingCaseId <> i.SourcingCaseId
                        OR bid.BusinessPartnerId <> i.BusinessPartnerId
                        OR tender.SourcePurchaseRequisitionId <> i.SourceRequisitionId
                        OR tender.SourcingReleaseId <> i.SourcingReleaseId
                        OR readiness.SourceType <> 2
                        OR readiness.SourceId <> exceptional.TenderId))
                    THROW 51210, 'The approved-exception source no longer authorizes this purchase order.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    LEFT JOIN ProcurementFrameworkCallOffs callOff
                        ON callOff.Id = i.ProcurementSourceId
                       AND callOff.TenantId = i.TenantId
                       AND callOff.PurchaseOrderId = i.Id
                       AND callOff.IsDeleted = 0
                    WHERE i.ProcurementSourceType = 4
                      AND ISNULL(d.Status, '') <> ISNULL(i.Status, '')
                      AND i.Status IN (
                          'Submitted', 'Pending Approval',
                          'Approved', 'Sent', 'Acknowledged')
                      AND (callOff.Id IS NULL OR callOff.Status IN (4, 5)))
                    THROW 51211, 'The framework call-off source no longer authorizes this purchase order.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS [TR_ProcurementFrameworkCallOffs_PurchaseOrderSource];
            DROP TRIGGER IF EXISTS [TR_PurchaseOrders_ApprovedSourceProtected];
            """);

        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseOrders_ApprovedSourceLineage",
            table: "PurchaseOrders");

        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseOrders_ProcurementAwardReadinessDecisions_AwardReadinessDecisionId",
            table: "PurchaseOrders");

        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseOrders_ProcurementSourcingCases_SourcingCaseId",
            table: "PurchaseOrders");

        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseOrders_ProcurementRequisitionSourcingReleases_SourcingReleaseId",
            table: "PurchaseOrders");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrders_AwardReadinessDecisionId",
            table: "PurchaseOrders");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrders_SourcingCaseId",
            table: "PurchaseOrders");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrders_SourcingReleaseId",
            table: "PurchaseOrders");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrders_TenantId_ProcurementSourceType_ProcurementSourceId",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "AwardReadinessDecisionId",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "ProcurementSourceId",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "ProcurementSourceReference",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "ProcurementSourceType",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "SourceIntegrityHash",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "SourceSnapshotJson",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "SourceValidatedAtUtc",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "SourcingCaseId",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "SourcingReleaseId",
            table: "PurchaseOrders");
    }
}
