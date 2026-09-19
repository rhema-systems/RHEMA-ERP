using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260805122000_FixSupplierApplicantPaymentAttribution")]
public sealed class FixSupplierApplicantPaymentAttribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierOnboardingPayments_Protected];");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementSupplierOnboardingPayments_State",
            table: "ProcurementSupplierOnboardingPayments");
        migrationBuilder.AddColumn<Guid>(
            name: "SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments",
            type: "uniqueidentifier",
            nullable: true);

        // Earlier applicant JWTs borrowed a tenant administrator as their
        // NameIdentifier. Recover the real applicant-session lineage using the
        // applicant-only principal name and the session active at submission.
        migrationBuilder.Sql(
            """
            ;WITH ApplicantPaymentSessions AS
            (
                SELECT
                    payment.Id AS PaymentId,
                    session.Id AS SessionId,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY payment.Id
                        ORDER BY
                            CASE WHEN session.IssuedAtUtc <= payment.CreatedAt THEN 0 ELSE 1 END,
                            ABS(DATEDIFF_BIG(SECOND, session.IssuedAtUtc, payment.CreatedAt)),
                            session.IssuedAtUtc DESC
                    ) AS MatchRank
                FROM ProcurementSupplierOnboardingPayments payment
                INNER JOIN ProcurementSupplierApplicantAccesses accessRecord
                    ON accessRecord.TenantId = payment.TenantId
                   AND accessRecord.TokenId = payment.TokenId
                   AND accessRecord.IsDeleted = 0
                INNER JOIN ProcurementSupplierApplicantSessions session
                    ON session.TenantId = payment.TenantId
                   AND session.ApplicantAccessId = accessRecord.Id
                   AND session.IsDeleted = 0
                WHERE payment.IsDeleted = 0
                  AND payment.CreatedBy LIKE 'supplier-applicant:%'
            )
            UPDATE payment
               SET payment.SubmittedByApplicantSessionId = matched.SessionId,
                   payment.CreatedBy = 'Verified Supplier Applicant',
                   payment.CreatedById = NULL
            FROM ProcurementSupplierOnboardingPayments payment
            INNER JOIN ApplicantPaymentSessions matched
                ON matched.PaymentId = payment.Id
               AND matched.MatchRank = 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementSupplierOnboardingPayments_SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments",
            column: "SubmittedByApplicantSessionId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementSupplierOnboardingPayments_TenantId_SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments",
            columns: new[] { "TenantId", "SubmittedByApplicantSessionId" });
        migrationBuilder.AddForeignKey(
            name: "FK_ProcurementSupplierOnboardingPayments_ProcurementSupplierApplicantSessions_SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments",
            column: "SubmittedByApplicantSessionId",
            principalTable: "ProcurementSupplierApplicantSessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementSupplierOnboardingPayments_State",
            table: "ProcurementSupplierOnboardingPayments",
            sql: CurrentStateCheck);
        migrationBuilder.Sql(AsDynamicSql(CurrentTrigger));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierOnboardingPayments_Protected];");
        migrationBuilder.DropForeignKey(
            name: "FK_ProcurementSupplierOnboardingPayments_ProcurementSupplierApplicantSessions_SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementSupplierOnboardingPayments_SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementSupplierOnboardingPayments_TenantId_SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementSupplierOnboardingPayments_State",
            table: "ProcurementSupplierOnboardingPayments");

        migrationBuilder.Sql(
            """
            UPDATE payment
               SET payment.CreatedById = COALESCE(session.CreatedById, accessRecord.CreatedById),
                   payment.CreatedBy = COALESCE(NULLIF(payment.CreatedBy, ''), 'Verified Supplier Applicant')
            FROM ProcurementSupplierOnboardingPayments payment
            INNER JOIN ProcurementSupplierApplicantSessions session
                ON session.Id = payment.SubmittedByApplicantSessionId
               AND session.TenantId = payment.TenantId
            INNER JOIN ProcurementSupplierApplicantAccesses accessRecord
                ON accessRecord.Id = session.ApplicantAccessId
               AND accessRecord.TenantId = payment.TenantId
            WHERE payment.CreatedById IS NULL;
            """);
        migrationBuilder.DropColumn(
            name: "SubmittedByApplicantSessionId",
            table: "ProcurementSupplierOnboardingPayments");
        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementSupplierOnboardingPayments_State",
            table: "ProcurementSupplierOnboardingPayments",
            sql: PreviousStateCheck);
        migrationBuilder.Sql(AsDynamicSql(PreviousTrigger));
    }

    private const string PreviousStateCheck =
        "[Status] BETWEEN 1 AND 5 AND [FeeAmount] >= 0 AND [TaxAmount] >= 0 " +
        "AND [TotalAmount] > 0 AND [TotalAmount] = [FeeAmount] + [TaxAmount] " +
        "AND LEN([CurrencyCode]) = 3 AND LEN([IntegrityHash]) = 64 " +
        "AND (([Status] IN (2, 3) AND [PostedAtUtc] IS NOT NULL " +
        "AND [PostingEventId] IS NOT NULL AND [JournalEntryId] IS NOT NULL " +
        "AND [ReceiptNumber] IS NOT NULL AND [ReceiptIssuedAtUtc] IS NOT NULL) " +
        "OR [Status] NOT IN (2, 3)) " +
        "AND (([Status] = 3 AND [ReconciledAtUtc] IS NOT NULL AND [ReconciledById] IS NOT NULL " +
        "AND [ReconciliationReference] IS NOT NULL) OR [Status] <> 3)";

    private const string CurrentStateCheck = PreviousStateCheck +
        " AND ([SubmittedByApplicantSessionId] IS NULL OR [CreatedById] IS NULL)";

    private static string AsDynamicSql(string command) =>
        $"EXEC(N'{command.Replace("'", "''", StringComparison.Ordinal)}')";

    private const string CurrentTrigger =
        """
        CREATE TRIGGER [dbo].[TR_ProcurementSupplierOnboardingPayments_Protected]
        ON [dbo].[ProcurementSupplierOnboardingPayments]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51810, 'Supplier-onboarding payments cannot be physically deleted.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.TenantId <> d.TenantId OR i.TokenId <> d.TokenId
                   OR i.PaymentMethodId <> d.PaymentMethodId
                   OR i.PaymentMethodCode <> d.PaymentMethodCode
                   OR i.PaymentMethodName <> d.PaymentMethodName
                   OR ISNULL(i.PaymentReference, '') <> ISNULL(d.PaymentReference, '')
                   OR i.FeeAmount <> d.FeeAmount OR i.TaxAmount <> d.TaxAmount
                   OR i.TotalAmount <> d.TotalAmount OR i.CurrencyCode <> d.CurrencyCode
                   OR (i.PaidAtUtc <> d.PaidAtUtc AND NOT (d.Status = 1 AND i.Status = 2))
                   OR i.CreationCorrelationId <> d.CreationCorrelationId
                   OR i.CreatedAt <> d.CreatedAt
                   OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                   OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                      <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                   OR ISNULL(i.SubmittedByApplicantSessionId, '00000000-0000-0000-0000-000000000000')
                      <> ISNULL(d.SubmittedByApplicantSessionId, '00000000-0000-0000-0000-000000000000')
                   OR i.IsDeleted <> d.IsDeleted)
                THROW 51811, 'Supplier-onboarding payment source and amount lineage is immutable.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE NOT (i.Status = d.Status OR
                           (d.Status = 1 AND i.Status IN (2, 5)) OR
                           (d.Status = 2 AND i.Status = 3)))
                THROW 51812, 'Invalid supplier-onboarding payment transition.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE (d.PostingEventId IS NOT NULL AND
                       (i.PostingEventId <> d.PostingEventId OR i.JournalEntryId <> d.JournalEntryId
                        OR i.PostedAtUtc <> d.PostedAtUtc OR i.ReceiptNumber <> d.ReceiptNumber
                        OR i.ReceiptIssuedAtUtc <> d.ReceiptIssuedAtUtc))
                   OR (d.ReconciledAtUtc IS NOT NULL AND
                       (i.ReconciledAtUtc <> d.ReconciledAtUtc
                        OR i.ReconciledById <> d.ReconciledById
                        OR i.ReconciliationReference <> d.ReconciliationReference
                        OR ISNULL(i.ReconciliationNotes, '') <> ISNULL(d.ReconciliationNotes, ''))))
                THROW 51813, 'Posted and reconciled supplier-onboarding payment lineage is immutable.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN ProcurementSupplierOnboardingTokens tokenRecord
                  ON tokenRecord.Id = i.TokenId AND tokenRecord.TenantId = i.TenantId
                LEFT JOIN PaymentMethod paymentMethod
                  ON paymentMethod.Id = i.PaymentMethodId AND paymentMethod.TenantId = i.TenantId
                LEFT JOIN ProcurementSupplierApplicantSessions applicantSession
                  ON applicantSession.Id = i.SubmittedByApplicantSessionId
                 AND applicantSession.TenantId = i.TenantId
                LEFT JOIN ProcurementSupplierApplicantAccesses applicantAccess
                  ON applicantAccess.Id = applicantSession.ApplicantAccessId
                 AND applicantAccess.TenantId = i.TenantId
                 AND applicantAccess.TokenId = i.TokenId
                WHERE tokenRecord.Id IS NULL OR paymentMethod.Id IS NULL
                   OR (i.SubmittedByApplicantSessionId IS NOT NULL AND applicantAccess.Id IS NULL))
                THROW 51814, 'Supplier-onboarding payment tenant or applicant-session lineage is invalid.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN FinancePostingEvents financeEvent
                  ON financeEvent.Id = i.PostingEventId AND financeEvent.TenantId = i.TenantId
                LEFT JOIN JournalEntries journalEntry
                  ON journalEntry.Id = i.JournalEntryId AND journalEntry.TenantId = i.TenantId
                WHERE i.Status IN (2, 3) AND (financeEvent.Id IS NULL OR journalEntry.Id IS NULL))
                THROW 51815, 'Supplier-onboarding payment Finance posting lineage is invalid.', 1;
        END
        """;

    private const string PreviousTrigger =
        """
        CREATE TRIGGER [dbo].[TR_ProcurementSupplierOnboardingPayments_Protected]
        ON [dbo].[ProcurementSupplierOnboardingPayments]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51810, 'Supplier-onboarding payments cannot be physically deleted.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.TenantId <> d.TenantId OR i.TokenId <> d.TokenId
                   OR i.PaymentMethodId <> d.PaymentMethodId
                   OR i.PaymentMethodCode <> d.PaymentMethodCode
                   OR i.PaymentMethodName <> d.PaymentMethodName
                   OR ISNULL(i.PaymentReference, '') <> ISNULL(d.PaymentReference, '')
                   OR i.FeeAmount <> d.FeeAmount OR i.TaxAmount <> d.TaxAmount
                   OR i.TotalAmount <> d.TotalAmount OR i.CurrencyCode <> d.CurrencyCode
                   OR i.PaidAtUtc <> d.PaidAtUtc
                   OR i.CreationCorrelationId <> d.CreationCorrelationId
                   OR i.CreatedAt <> d.CreatedAt
                   OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                   OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                      <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                   OR i.IsDeleted <> d.IsDeleted)
                THROW 51811, 'Supplier-onboarding payment source and amount lineage is immutable.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE NOT (i.Status = d.Status OR
                           (d.Status = 1 AND i.Status IN (2, 5)) OR
                           (d.Status = 2 AND i.Status = 3)))
                THROW 51812, 'Invalid supplier-onboarding payment transition.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE (d.PostingEventId IS NOT NULL AND
                       (i.PostingEventId <> d.PostingEventId OR i.JournalEntryId <> d.JournalEntryId
                        OR i.PostedAtUtc <> d.PostedAtUtc OR i.ReceiptNumber <> d.ReceiptNumber
                        OR i.ReceiptIssuedAtUtc <> d.ReceiptIssuedAtUtc))
                   OR (d.ReconciledAtUtc IS NOT NULL AND
                       (i.ReconciledAtUtc <> d.ReconciledAtUtc OR i.ReconciledById <> d.ReconciledById
                        OR i.ReconciliationReference <> d.ReconciliationReference
                        OR ISNULL(i.ReconciliationNotes, '') <> ISNULL(d.ReconciliationNotes, ''))))
                THROW 51813, 'Posted and reconciled supplier-onboarding payment lineage is immutable.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN ProcurementSupplierOnboardingTokens tokenRecord
                  ON tokenRecord.Id = i.TokenId AND tokenRecord.TenantId = i.TenantId
                LEFT JOIN PaymentMethod paymentMethod
                  ON paymentMethod.Id = i.PaymentMethodId AND paymentMethod.TenantId = i.TenantId
                WHERE tokenRecord.Id IS NULL OR paymentMethod.Id IS NULL)
                THROW 51814, 'Supplier-onboarding payment tenant lineage is invalid.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN FinancePostingEvents financeEvent
                  ON financeEvent.Id = i.PostingEventId AND financeEvent.TenantId = i.TenantId
                LEFT JOIN JournalEntries journalEntry
                  ON journalEntry.Id = i.JournalEntryId AND journalEntry.TenantId = i.TenantId
                WHERE i.Status IN (2, 3) AND (financeEvent.Id IS NULL OR journalEntry.Id IS NULL))
                THROW 51815, 'Supplier-onboarding payment Finance posting lineage is invalid.', 1;
        END
        """;
}
