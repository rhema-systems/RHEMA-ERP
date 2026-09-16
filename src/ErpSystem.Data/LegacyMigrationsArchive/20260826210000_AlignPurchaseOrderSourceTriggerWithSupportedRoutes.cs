using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Keeps the SQL trigger aligned with the direct and advanced purchase-order
/// source routes already enforced by CK_PurchaseOrders_ApprovedSourceLineage.
/// The trigger is patched in place so the TDC-0406 amendment exception remains
/// intact.
/// </summary>
public partial class AlignPurchaseOrderSourceTriggerWithSupportedRoutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(BuildTriggerPatchSql(
            SupportedSourceValidationBlock,
            SupportedCurrentLineageBlock));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(BuildTriggerPatchSql(
            StrictSourceValidationBlock,
            StrictCurrentLineageBlock));
    }

    private static string BuildTriggerPatchSql(
        string sourceValidationBlock,
        string currentLineageBlock)
    {
        var escapedSourceValidation = EscapeSqlLiteral(sourceValidationBlock);
        var escapedCurrentLineage = EscapeSqlLiteral(currentLineageBlock);

        return $$"""
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_PurchaseOrders_ApprovedSourceProtected]'));
            IF @definition IS NULL
                THROW 51440, 'The approved-source protection trigger is required before source-route alignment can be installed.', 1;
            IF CHARINDEX(N'TDC0406_PO_AMENDMENT_ID', @definition) = 0
                THROW 51441, 'The approved-source amendment protection must remain installed during source-route alignment.', 1;

            DECLARE @marker51202 int = CHARINDEX(N'THROW 51202', @definition);
            DECLARE @start51202 int = 0;
            DECLARE @candidate51202 int = CHARINDEX(N'IF EXISTS (', @definition);
            WHILE @candidate51202 > 0 AND @candidate51202 < @marker51202
            BEGIN
                SET @start51202 = @candidate51202;
                SET @candidate51202 = CHARINDEX(N'IF EXISTS (', @definition, @candidate51202 + 1);
            END;
            DECLARE @throw51202 nvarchar(300) =
                N'THROW 51202, ''A complete immutable approved source lineage is required for every purchase order.'', 1;';
            DECLARE @finish51202 int = CHARINDEX(@throw51202, @definition, @start51202);
            IF @marker51202 = 0 OR @start51202 = 0 OR @finish51202 = 0
                THROW 51442, 'The purchase-order source validation guard could not be located safely.', 1;
            SET @finish51202 = @finish51202 + LEN(@throw51202);
            SET @definition = STUFF(
                @definition,
                @start51202,
                @finish51202 - @start51202,
                N'{{escapedSourceValidation}}');

            DECLARE @marker51205 int = CHARINDEX(N'THROW 51205', @definition);
            DECLARE @start51205 int = 0;
            DECLARE @candidate51205 int = CHARINDEX(N'IF EXISTS (', @definition);
            WHILE @candidate51205 > 0 AND @candidate51205 < @marker51205
            BEGIN
                SET @start51205 = @candidate51205;
                SET @candidate51205 = CHARINDEX(N'IF EXISTS (', @definition, @candidate51205 + 1);
            END;
            DECLARE @throw51205 nvarchar(300) =
                N'THROW 51205, ''The approved requisition, sourcing release, sourcing case, or award-readiness decision is invalid.'', 1;';
            DECLARE @finish51205 int = CHARINDEX(@throw51205, @definition, @start51205);
            IF @marker51205 = 0 OR @start51205 = 0 OR @finish51205 = 0
                THROW 51443, 'The purchase-order current-lineage guard could not be located safely.', 1;
            SET @finish51205 = @finish51205 + LEN(@throw51205);
            SET @definition = STUFF(
                @definition,
                @start51205,
                @finish51205 - @start51205,
                N'{{escapedCurrentLineage}}');

            DECLARE @triggerKeywordPosition int =
                CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerKeywordPosition = 0
                THROW 51444, 'The approved-source trigger declaration could not be altered safely.', 1;
            SET @definition =
                N'ALTER ' + SUBSTRING(
                    @definition,
                    @triggerKeywordPosition,
                    LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """;
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    private const string SupportedSourceValidationBlock = """
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
                   OR NOT (
                          (i.SourcingCaseId IS NOT NULL
                           AND i.AwardReadinessDecisionId IS NOT NULL)
                       OR (i.SourcingCaseId IS NULL
                           AND i.ProcurementSourceType = 0
                           AND i.AwardReadinessDecisionId IS NULL)
                       OR (i.SourcingCaseId IS NULL
                           AND i.ProcurementSourceType IN (1, 2)
                           AND i.AwardReadinessDecisionId IS NOT NULL)))))
            THROW 51202, 'A complete immutable approved source lineage is required for every purchase order.', 1;
        """;

    private const string SupportedCurrentLineageBlock = """
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
                OR (i.SourcingCaseId IS NOT NULL AND (
                       sourcing.Id IS NULL
                    OR sourcing.Status = 3))
                OR (i.AwardReadinessDecisionId IS NOT NULL
                    AND readiness.Id IS NULL)))
            THROW 51205, 'The approved requisition, sourcing release, sourcing case, or award-readiness decision is invalid.', 1;
        """;

    private const string StrictSourceValidationBlock = """
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
        """;

    private const string StrictCurrentLineageBlock = """
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
        """;
}
