using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Allows the controlled tender evaluation/award recovery path to attach the
/// exact sourcing case already locked to a tender's immutable requisition
/// release. Existing non-null lineage remains immutable.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260901033000_AllowGovernedTenderLineageRecovery")]
public sealed class AllowGovernedTenderLineageRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateTenderSourcingReleaseGuard(migrationBuilder, allowGovernedRecovery: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        CreateTenderSourcingReleaseGuard(migrationBuilder, allowGovernedRecovery: false);

    private static void CreateTenderSourcingReleaseGuard(
        MigrationBuilder migrationBuilder,
        bool allowGovernedRecovery)
    {
        var sourcingCaseImmutability = allowGovernedRecovery
            ? """
              OR (d.[SourcingCaseId] IS NOT NULL AND
                  (i.[SourcingCaseId] IS NULL OR d.[SourcingCaseId] <> i.[SourcingCaseId]))
              """
            : """
              OR ISNULL(CONVERT(nvarchar(36), d.[SourcingCaseId]), '') <>
                 ISNULL(CONVERT(nvarchar(36), i.[SourcingCaseId]), '')
              """;

        migrationBuilder.Sql($$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_Tenders_SourcingReleaseGuard]
            ON [dbo].[Tenders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                    LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[SourcePurchaseRequisitionId]
                    LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId]
                    WHERE
                        (i.[SourcePurchaseRequisitionId] IS NULL AND
                            (i.[SourcingReleaseId] IS NOT NULL OR i.[SourcingCaseId] IS NOT NULL))
                        OR (i.[SourcePurchaseRequisitionId] IS NOT NULL AND i.[SourcingReleaseId] IS NULL)
                        OR (i.[SourcePurchaseRequisitionId] IS NOT NULL AND
                            (i.[EstimatedValue] IS NULL OR NULLIF(LTRIM(RTRIM(i.[Currency])), '') IS NULL
                             OR pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1 OR pr.[Status] <> 'Approved'
                             OR sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1
                             OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]
                             OR ISNULL(pr.[TotalAmount], -1) <> ISNULL(i.[EstimatedValue], -1)
                             OR UPPER(LTRIM(RTRIM(pr.[Currency]))) <> UPPER(LTRIM(RTRIM(i.[Currency])))))
                        OR (i.[SourcingCaseId] IS NOT NULL AND
                            (sc.[Id] IS NULL OR sc.[TenantId] <> i.[TenantId] OR sc.[IsDeleted] = 1 OR sc.[Status] NOT IN (0, 1)
                             OR sc.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]
                             OR sc.[SourcingReleaseId] <> i.[SourcingReleaseId]
                             OR (UPPER(LTRIM(RTRIM(i.[TenderType]))) = 'RFQ' AND sc.[SelectedMethod] <> 0)
                             OR (UPPER(LTRIM(RTRIM(i.[TenderType]))) <> 'RFQ' AND sc.[SelectedMethod] = 0)
                             OR ISNULL(sc.[EstimatedValue], -1) <> ISNULL(i.[EstimatedValue], -1)
                             OR UPPER(LTRIM(RTRIM(sc.[CurrencyCode]))) <> UPPER(LTRIM(RTRIM(i.[Currency])))
                             OR sc.[SourceControlFingerprint] <> sr.[ControlFingerprint]))
                        OR (d.[Id] IS NOT NULL AND d.[SourcePurchaseRequisitionId] IS NOT NULL AND
                            (i.[SourcePurchaseRequisitionId] IS NULL OR d.[SourcePurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]
                             OR i.[SourcingReleaseId] IS NULL OR d.[SourcingReleaseId] <> i.[SourcingReleaseId]
                             {{sourcingCaseImmutability}}
                             OR ISNULL(d.[EstimatedValue], -1) <> ISNULL(i.[EstimatedValue], -1)
                             OR ISNULL(d.[Currency], '') <> ISNULL(i.[Currency], '')
                             OR d.[TenderType] <> i.[TenderType]))
                )
                    THROW 51070, 'PR-linked Tender requires an approved requisition, its sourcing-release audit record, and a matching optional sourcing case.', 1;
            END
            """);
    }
}
