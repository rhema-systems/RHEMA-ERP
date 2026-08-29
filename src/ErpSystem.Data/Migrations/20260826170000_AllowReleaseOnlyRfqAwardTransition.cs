using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Aligns the statutory RFQ lifecycle trigger with the supported direct
/// approved-PR route. Advanced sourcing-case RFQs retain their staged
/// evaluation and approval transitions.
/// </summary>
public partial class AllowReleaseOnlyRfqAwardTransition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateGuard(migrationBuilder, allowReleaseOnlyDirectAward: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        CreateGuard(migrationBuilder, allowReleaseOnlyDirectAward: false);

    private static void CreateGuard(
        MigrationBuilder migrationBuilder,
        bool allowReleaseOnlyDirectAward)
    {
        var releaseOnlyAwardTransition = allowReleaseOnlyDirectAward
            ? """
              i.[Status] = 'Awarded'
              AND i.[SourcePurchaseRequisitionId] IS NOT NULL
              AND i.[SourcingReleaseId] IS NOT NULL
              AND i.[SourcingCaseId] IS NULL
              AND i.[SubmissionDeadline] IS NOT NULL
              AND i.[SubmissionDeadline] <= SYSUTCDATETIME()
              AND i.[AwardedAt] IS NOT NULL
              AND EXISTS
                  (
                      SELECT 1
                      FROM [dbo].[RequestForQuotationAwardLines] al
                      WHERE al.[RfqId] = i.[Id]
                        AND al.[TenantId] = i.[TenantId]
                        AND al.[IsDeleted] = 0
                  )
              """
            : "1 = 0";

        migrationBuilder.Sql($$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_RequestForQuotations_StatutoryLifecycleGuard]
            ON [dbo].[RequestForQuotations]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE (d.Status <> 'Draft' AND
                          ((ISNULL(i.SubmissionDeadline, '19000101') <> ISNULL(d.SubmissionDeadline, '19000101')
                            AND
                            (
                                i.SubmissionDeadline IS NULL OR d.SubmissionDeadline IS NULL
                                OR i.SubmissionDeadline <= d.SubmissionDeadline
                                OR NOT EXISTS
                                   (
                                       SELECT 1
                                       FROM [dbo].[ProcurementTenderDocumentRegisters] r
                                       JOIN [dbo].[ProcurementTenderDocumentChanges] c
                                         ON c.[RegisterId] = r.[Id]
                                        AND c.[TenantId] = r.[TenantId]
                                        AND c.[IsDeleted] = 0
                                       LEFT JOIN [dbo].[WorkflowInstances] wi
                                         ON wi.[Id] = c.[WorkflowInstanceId]
                                        AND wi.[TenantId] = c.[TenantId]
                                        AND wi.[WorkflowDefinitionId] = c.[WorkflowDefinitionId]
                                        AND wi.[EntityId] = c.[Id]
                                        AND wi.[IsDeleted] = 0
                                       WHERE r.[RequestForQuotationId] = i.[Id]
                                         AND r.[TenantId] = i.[TenantId]
                                         AND r.[SourceType] = 1
                                         AND r.[IsDeleted] = 0
                                         AND c.[ChangeType] = 1
                                         AND c.[PreviousValueUtc] = d.[SubmissionDeadline]
                                         AND c.[NewValueUtc] = i.[SubmissionDeadline]
                                         AND
                                            (
                                                (c.[Status] = 1
                                                 AND c.[WorkflowOutcome] = 'Approved')
                                                OR
                                                (c.[Status] = 0
                                                 AND wi.[Id] IS NOT NULL
                                                 AND wi.[Status] = 2)
                                            )
                                   )
                            ))
                           OR i.Title <> d.Title OR ISNULL(i.Description, '') <> ISNULL(d.Description, '')
                           OR ISNULL(i.ExternalRecipientEmails, '') <> ISNULL(d.ExternalRecipientEmails, '')
                           OR i.Currency <> d.Currency OR i.EstimatedValue <> d.EstimatedValue
                           OR i.IsDeleted <> d.IsDeleted))
                       OR (d.Status = 'Draft' AND i.Status NOT IN ('Draft','Sent','Cancelled'))
                       OR (d.Status = 'Sent'
                           AND i.Status NOT IN ('Sent','Evaluation','Cancelled')
                           AND NOT ({{releaseOnlyAwardTransition}}))
                       OR (d.Status = 'Evaluation' AND i.Status NOT IN ('Evaluation','PendingApproval','Cancelled'))
                       OR (d.Status = 'PendingApproval' AND i.Status NOT IN ('PendingApproval','Approved','Rejected'))
                       OR (d.Status = 'Approved' AND i.Status NOT IN ('Approved','Awarded'))
                       OR (d.Status IN ('Awarded','Rejected','Cancelled','Closed') AND i.Status <> d.Status))
                    THROW 51094, 'RFQ issue terms or statutory lifecycle transition is invalid.', 1;
            END
            """);
    }
}
