SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @PaymentReference nvarchar(100) = N'QS-E2E-FINAL-ACCEPTANCE';
DECLARE @CertificateNumber nvarchar(100) = N'IPC-2026-00003';
DECLARE @PaymentId uniqueidentifier;
DECLARE @InvoiceId uniqueidentifier;
DECLARE @OriginalJournalId uniqueidentifier;
DECLARE @ReversalJournalId uniqueidentifier;

IF (SELECT COUNT(*) FROM VendorPayment
    WHERE TenantId = @TenantId AND TransactionReference = @PaymentReference AND IsDeleted = 0) <> 1
    THROW 52601, 'QS final acceptance requires exactly one governed payment reference.', 1;

SELECT @PaymentId = Id,
       @OriginalJournalId = JournalEntryId,
       @ReversalJournalId = ReversalJournalEntryId
FROM VendorPayment
WHERE TenantId = @TenantId AND TransactionReference = @PaymentReference AND IsDeleted = 0;

SELECT TOP (1) @InvoiceId = VendorInvoiceId
FROM VendorPaymentAllocation
WHERE TenantId = @TenantId AND VendorPaymentId = @PaymentId AND IsReversal = 0 AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM VendorPayment
               WHERE TenantId = @TenantId AND Id = @PaymentId AND Status = 9
                 AND AllocatedAmount = 0 AND ReversalPostingEventId IS NOT NULL
                 AND JournalEntryId IS NOT NULL AND ReversalJournalEntryId IS NOT NULL)
    THROW 52602, 'QS payment did not reach the durable Reversed lifecycle state.', 1;

IF (SELECT COUNT(*) FROM JournalEntries
    WHERE TenantId = @TenantId AND Id IN (@OriginalJournalId, @ReversalJournalId)
      AND PostingStatus = N'Posted' AND IsBalanced = 1
      AND TotalDebitAmount = TotalCreditAmount AND BalanceDifference = 0) <> 2
    THROW 52603, 'The original or reversal GL journal is not posted and balanced.', 1;

IF NOT EXISTS (SELECT 1 FROM JournalEntries
               WHERE TenantId = @TenantId AND Id = @OriginalJournalId
                 AND IsReversed = 1 AND ReversalJournalEntryId = @ReversalJournalId)
   OR NOT EXISTS (SELECT 1 FROM JournalEntries
                  WHERE TenantId = @TenantId AND Id = @ReversalJournalId
                    AND OriginalJournalEntryId = @OriginalJournalId)
    THROW 52604, 'The original and compensating journals do not have reciprocal reversal lineage.', 1;

IF EXISTS (
    SELECT AccountId
    FROM AccountTransactions
    WHERE TenantId = @TenantId
      AND JournalEntryId IN (@OriginalJournalId, @ReversalJournalId)
      AND IsDeleted = 0
    GROUP BY AccountId
    HAVING SUM(DebitAmount - CreditAmount) <> 0)
    THROW 52605, 'The payment and reversal do not net to zero by GL account.', 1;

IF NOT EXISTS (SELECT 1 FROM FinancePostingEvents
               WHERE TenantId = @TenantId AND SourceDocumentType = N'VendorPayment'
                 AND SourceDocumentId = @PaymentId AND PostingAction = N'Post'
                 AND PostingStatus = N'Posted' AND JournalEntryId = @OriginalJournalId
                 AND TotalDebitAmount = TotalCreditAmount)
   OR NOT EXISTS (SELECT 1 FROM FinancePostingEvents
                  WHERE TenantId = @TenantId AND SourceDocumentType = N'VendorPayment'
                    AND SourceDocumentId = @PaymentId AND PostingAction = N'Reverse'
                    AND PostingStatus = N'Posted' AND JournalEntryId = @ReversalJournalId
                    AND TotalDebitAmount = TotalCreditAmount)
    THROW 52606, 'The central Finance posting events do not prove both post and reversal.', 1;

IF (SELECT COALESCE(SUM(AllocatedAmount), 0) FROM VendorPaymentAllocation
    WHERE TenantId = @TenantId AND VendorPaymentId = @PaymentId AND IsDeleted = 0) <> 0
   OR (SELECT COUNT(*) FROM VendorPaymentAllocation
       WHERE TenantId = @TenantId AND VendorPaymentId = @PaymentId AND IsReversal = 1 AND IsDeleted = 0) <> 1
    THROW 52607, 'The invoice allocation did not reverse exactly once and net to zero.', 1;

IF NOT EXISTS (SELECT 1 FROM VendorInvoice
               WHERE TenantId = @TenantId AND Id = @InvoiceId
                 AND Status = 3 AND PaidAmount = 0 AND TotalAmount = 1510500.00)
    THROW 52608, 'The QS-linked AP invoice was not restored to Approved and fully outstanding.', 1;

IF NOT EXISTS (SELECT 1 FROM ProjectPaymentCertificates
               WHERE TenantId = @TenantId AND CertificateNumber = @CertificateNumber
                 AND VendorInvoiceId = @InvoiceId AND NetCertifiedAmount = 1510500.00
                 AND IsDeleted = 0)
    THROW 52609, 'The payment certificate no longer has exact QS-to-AP invoice lineage.', 1;

IF (SELECT COUNT(*) FROM AuditLogs
    WHERE TenantId = @TenantId AND Action = N'QuantitySurveyAuthorizationDenied'
      AND Timestamp >= DATEADD(hour, -4, SYSUTCDATETIME()) AND IsDeleted = 0) < 7
    THROW 52610, 'The wider QS direct-API authorization matrix is not present in the audit trail.', 1;

SELECT
    N'PASS' AS GateStatus,
    payment.PaymentNumber,
    payment.Id AS PaymentId,
    payment.JournalEntryId,
    payment.ReversalJournalEntryId,
    original.TotalDebitAmount AS OriginalDebit,
    original.TotalCreditAmount AS OriginalCredit,
    reversal.TotalDebitAmount AS ReversalDebit,
    reversal.TotalCreditAmount AS ReversalCredit,
    invoice.InvoiceNumber,
    invoice.TotalAmount AS RestoredInvoiceBalance,
    certificate.CertificateNumber,
    certificate.NetCertifiedAmount,
    (SELECT COUNT(*) FROM AuditLogs
     WHERE TenantId = @TenantId AND Action = N'QuantitySurveyAuthorizationDenied'
       AND Timestamp >= DATEADD(hour, -4, SYSUTCDATETIME()) AND IsDeleted = 0) AS RecentDeniedAuditCount
FROM VendorPayment payment
JOIN JournalEntries original ON original.Id = payment.JournalEntryId AND original.TenantId = payment.TenantId
JOIN JournalEntries reversal ON reversal.Id = payment.ReversalJournalEntryId AND reversal.TenantId = payment.TenantId
JOIN VendorInvoice invoice ON invoice.Id = @InvoiceId AND invoice.TenantId = payment.TenantId
JOIN ProjectPaymentCertificates certificate ON certificate.VendorInvoiceId = invoice.Id
    AND certificate.TenantId = payment.TenantId AND certificate.CertificateNumber = @CertificateNumber
WHERE payment.Id = @PaymentId AND payment.TenantId = @TenantId;
