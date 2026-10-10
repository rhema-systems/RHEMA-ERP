:setvar TenantId "00000000-0000-0000-0000-000000000000"
:setvar FromUtc "2000-01-01T00:00:00Z"
:setvar ToUtc "2100-01-01T00:00:00Z"
:setvar AllowNoCompletedSales "0"
:setvar ExpectedDatabase "RhemaERP"

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

DECLARE @TenantId uniqueidentifier = TRY_CONVERT(uniqueidentifier, '$(TenantId)');
DECLARE @FromUtc datetime2 = TRY_CONVERT(datetime2, '$(FromUtc)', 127);
DECLARE @ToUtc datetime2 = TRY_CONVERT(datetime2, '$(ToUtc)', 127);
DECLARE @AllowNoCompletedSales bit = TRY_CONVERT(bit, '$(AllowNoCompletedSales)');

IF DB_NAME() <> '$(ExpectedDatabase)'
    THROW 51000, 'The connected database does not match the requested certification database.', 1;
IF @TenantId IS NULL OR @TenantId = '00000000-0000-0000-0000-000000000000'
    THROW 51000, 'A non-empty tenant ID is required.', 1;
IF @FromUtc IS NULL OR @ToUtc IS NULL OR @FromUtc >= @ToUtc
    THROW 51000, 'A valid half-open UTC certification window is required.', 1;
IF @AllowNoCompletedSales IS NULL
    THROW 51000, 'AllowNoCompletedSales must be 0 or 1.', 1;

DECLARE @CompletedSaleCount int = (
    SELECT COUNT(*)
    FROM dbo.MobilePosSales s
    WHERE s.TenantId = @TenantId
      AND s.IsDeleted = 0
      AND s.Status = 2
      AND s.OccurredAtUtc >= @FromUtc
      AND s.OccurredAtUtc < @ToUtc
);
IF @CompletedSaleCount = 0 AND @AllowNoCompletedSales = 0
    THROW 51000, 'The certification window contains no completed Mobile POS sale.', 1;

DECLARE @Findings TABLE
(
    FindingId int IDENTITY(1,1) PRIMARY KEY,
    ControlCode varchar(40) NOT NULL,
    RecordId uniqueidentifier NULL,
    Reference nvarchar(150) NULL,
    Detail nvarchar(1000) NOT NULL
);

-- A completed source envelope must be arithmetically sound and linked to canonical Finance output.
INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-001', s.Id, s.LocalReference,
       'Completed sale header is missing an invoice/sync timestamp or its subtotal, tax, discount and total do not reconcile.'
FROM dbo.MobilePosSales s
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (s.InvoiceId IS NULL OR s.SynchronizedAtUtc IS NULL
       OR ABS((s.SubTotal + s.TaxAmount - s.DiscountAmount) - s.TotalAmount) > 0.01);

INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-002', s.Id, s.LocalReference,
       'Persisted Mobile POS line totals do not reproduce the completed sale header.'
FROM dbo.MobilePosSales s
OUTER APPLY
(
    SELECT COUNT(*) LineCount,
           COALESCE(SUM(l.LineTotal), 0) SubTotal,
           COALESCE(SUM(l.TaxAmount), 0) TaxAmount,
           COALESCE(SUM(l.DiscountAmount), 0) DiscountAmount
    FROM dbo.MobilePosSaleLines l
    WHERE l.TenantId = s.TenantId AND l.MobilePosSaleId = s.Id AND l.IsDeleted = 0
) lines
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (lines.LineCount = 0 OR ABS(lines.SubTotal - s.SubTotal) > 0.01
       OR ABS(lines.TaxAmount - s.TaxAmount) > 0.01
       OR ABS(lines.DiscountAmount - s.DiscountAmount) > 0.01);

INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-003', s.Id, s.LocalReference,
       'Completed tender total does not equal the canonical sale total or contains a non-completed tender.'
FROM dbo.MobilePosSales s
OUTER APPLY
(
    SELECT COUNT(*) TenderCount,
           COALESCE(SUM(t.Amount), 0) TenderTotal,
           COALESCE(SUM(CASE WHEN t.Status <> 2 THEN 1 ELSE 0 END), 0) InvalidStatusCount
    FROM dbo.MobilePosTenders t
    WHERE t.TenantId = s.TenantId AND t.MobilePosSaleId = s.Id AND t.IsDeleted = 0
) tenders
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (tenders.TenderCount = 0 OR tenders.InvalidStatusCount <> 0
       OR ABS(tenders.TenderTotal - s.TotalAmount) > 0.01);

-- The Mobile POS envelope and canonical invoice must describe the same tenant, customer, currency and value.
INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-004', s.Id, s.LocalReference,
       'Canonical invoice is missing, unposted, cross-tenant, or differs from the Mobile POS sale snapshot.'
FROM dbo.MobilePosSales s
LEFT JOIN dbo.Invoices i ON i.Id = s.InvoiceId AND i.IsDeleted = 0
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (i.Id IS NULL OR i.TenantId <> s.TenantId OR i.BusinessPartnerId <> s.BusinessPartnerId
       OR i.BusinessPartnerRoleId <> s.BusinessPartnerRoleId OR i.CurrencyCode <> s.CurrencyCode
       OR ABS(i.SubTotal - s.SubTotal) > 0.01 OR ABS(i.TaxAmount - s.TaxAmount) > 0.01
       OR ABS((i.DiscountAmount + (SELECT COALESCE(SUM(il.DiscountAmount), 0)
                                   FROM dbo.InvoiceLineItem il
                                   WHERE il.InvoiceId = i.Id AND il.IsDeleted = 0)) - s.DiscountAmount) > 0.01
       OR ABS(i.TotalAmount - s.TotalAmount) > 0.01 OR i.JournalEntryId IS NULL
       OR i.SourceBookAuthorityId IS NULL);

-- Each split tender owns one posted CustomerPayment and one active allocation to this sale's invoice.
INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-005', t.Id, s.LocalReference,
       'Completed tender is missing its posted canonical CustomerPayment or the payment snapshot differs.'
FROM dbo.MobilePosSales s
JOIN dbo.MobilePosTenders t ON t.MobilePosSaleId = s.Id AND t.TenantId = s.TenantId AND t.IsDeleted = 0
LEFT JOIN dbo.CustomerPayment p ON p.Id = t.CustomerPaymentId AND p.IsDeleted = 0
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2 AND t.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (p.Id IS NULL OR p.TenantId <> s.TenantId OR p.BusinessPartnerId <> s.BusinessPartnerId
       OR p.BusinessPartnerRoleId <> s.BusinessPartnerRoleId OR p.PaymentMethodId <> t.PaymentMethodId
       OR p.CurrencyCode <> s.CurrencyCode OR ABS(p.TotalAmount - t.Amount) > 0.01
       OR ABS(p.AllocatedAmount - t.Amount) > 0.01 OR p.Status <> 'Posted'
       OR p.JournalEntryId IS NULL OR p.SourceBookAuthorityId IS NULL
       OR (p.BankAccountId IS NULL AND p.LiquidityAccountId IS NULL));

INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-006', t.Id, s.LocalReference,
       'Canonical CustomerPayment does not have exactly one active non-reversal allocation to the sale invoice for the tender amount.'
FROM dbo.MobilePosSales s
JOIN dbo.MobilePosTenders t ON t.MobilePosSaleId = s.Id AND t.TenantId = s.TenantId AND t.IsDeleted = 0
OUTER APPLY
(
    SELECT COUNT(*) AllocationCount,
           COALESCE(SUM(pa.AllocatedAmount), 0) AllocatedAmount,
           COALESCE(SUM(pa.PaymentCurrencyAmount), 0) PaymentCurrencyAmount
    FROM dbo.PaymentAllocation pa
    WHERE pa.TenantId = s.TenantId AND pa.CustomerPaymentId = t.CustomerPaymentId
      AND pa.InvoiceId = s.InvoiceId AND pa.IsDeleted = 0 AND pa.IsReversal = 0
) allocations
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2 AND t.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (allocations.AllocationCount <> 1 OR ABS(allocations.AllocatedAmount - t.Amount) > 0.01
       OR ABS(allocations.PaymentCurrencyAmount - t.Amount) > 0.01);

-- The durable mutation receipt must point back to exactly the same canonical result.
INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-007', s.Id, s.LocalReference,
       'Completed sale has no matching completed mutation receipt or its canonical links disagree.'
FROM dbo.MobilePosSales s
LEFT JOIN dbo.MobileMutationReceipts mr
  ON mr.TenantId = s.TenantId AND mr.MobilePosDeviceId = s.MobilePosDeviceId
 AND mr.ClientMutationId = s.ClientMutationId AND mr.IsDeleted = 0
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2
  AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc
  AND (mr.Id IS NULL OR mr.Status <> 2 OR mr.MobilePosSaleId <> s.Id
       OR mr.CanonicalInvoiceId <> s.InvoiceId OR mr.CompletedAtUtc IS NULL);

-- Day-end evidence may finalize only with a closed canonical till session and valid optional deposit link.
INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-008', c.Id, session.SessionNumber,
       'Finalized Mobile POS close is not linked to the same tenant/till closed Finance session or lacks finalization evidence.'
FROM dbo.MobilePosTillCloseSubmissions c
LEFT JOIN dbo.CashierTillSessions session ON session.Id = c.CashierTillSessionId AND session.IsDeleted = 0
LEFT JOIN dbo.MobilePosTills till ON till.Id = c.MobilePosTillId AND till.IsDeleted = 0
WHERE c.TenantId = @TenantId AND c.IsDeleted = 0 AND c.Status = 4
  AND c.SubmittedAtUtc >= @FromUtc AND c.SubmittedAtUtc < @ToUtc
  AND (session.Id IS NULL OR session.TenantId <> c.TenantId OR session.Status <> 3
       OR till.Id IS NULL OR till.TenantId <> c.TenantId OR till.LiquidityAccountId <> session.LiquidityAccountId
       OR c.FinalizedAtUtc IS NULL OR c.FinalizedByUserId IS NULL);

INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-009', c.Id, session.SessionNumber,
       'Mobile POS close deposit proposal is missing, cross-tenant, has no allocations, or does not retain its immutable source marker.'
FROM dbo.MobilePosTillCloseSubmissions c
JOIN dbo.CashierTillSessions session ON session.Id = c.CashierTillSessionId AND session.IsDeleted = 0
LEFT JOIN dbo.BankDepositBatches deposit ON deposit.Id = c.BankDepositBatchId AND deposit.IsDeleted = 0
OUTER APPLY
(
    SELECT COUNT(*) AllocationCount,
           COALESCE(SUM(CASE WHEN a.AllocationType = 2 THEN -a.Amount ELSE a.Amount END), 0) AllocationTotal
    FROM dbo.BankDepositAllocations a
    WHERE a.TenantId = c.TenantId AND a.BankDepositBatchId = c.BankDepositBatchId AND a.IsDeleted = 0
) allocations
WHERE c.TenantId = @TenantId AND c.IsDeleted = 0 AND c.BankDepositBatchId IS NOT NULL
  AND c.SubmittedAtUtc >= @FromUtc AND c.SubmittedAtUtc < @ToUtc
  AND (deposit.Id IS NULL OR deposit.TenantId <> c.TenantId OR allocations.AllocationCount = 0
       OR ABS(allocations.AllocationTotal - deposit.NetAmount) > 0.01
       OR c.BankDepositProposedAtUtc IS NULL OR c.BankDepositProposedByUserId IS NULL
       OR deposit.Notes IS NULL
       OR deposit.Notes NOT LIKE '%[[]MobilePosTillClose:' + REPLACE(CONVERT(varchar(36), c.Id), '-', '') + ']%');

-- Database uniqueness should already enforce these identities; these checks also detect legacy/manual drift.
INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-010', NULL, s.ClientMutationId,
       'Duplicate active tenant/device/client mutation identity exists.'
FROM dbo.MobilePosSales s
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0
GROUP BY s.TenantId, s.MobilePosDeviceId, s.ClientMutationId
HAVING COUNT(*) > 1;

INSERT INTO @Findings(ControlCode, RecordId, Reference, Detail)
SELECT 'MPOS-ACC-011', NULL, CONVERT(nvarchar(150), t.CustomerPaymentId),
       'One canonical CustomerPayment is linked to multiple active Mobile POS tenders.'
FROM dbo.MobilePosTenders t
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0 AND t.CustomerPaymentId IS NOT NULL
GROUP BY t.TenantId, t.CustomerPaymentId
HAVING COUNT(*) > 1;

SELECT ControlCode, COUNT(*) FindingCount
FROM @Findings
GROUP BY ControlCode
ORDER BY ControlCode;

SELECT FindingId, ControlCode, RecordId, Reference, Detail
FROM @Findings
ORDER BY FindingId;

SELECT
    @CompletedSaleCount AS CompletedSales,
    (SELECT COUNT(*) FROM dbo.MobilePosTenders t JOIN dbo.MobilePosSales s ON s.Id = t.MobilePosSaleId
     WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status = 2 AND t.IsDeleted = 0 AND t.Status = 2
       AND s.OccurredAtUtc >= @FromUtc AND s.OccurredAtUtc < @ToUtc) AS CompletedTenders,
    (SELECT COUNT(*) FROM dbo.MobilePosTillCloseSubmissions c
     WHERE c.TenantId = @TenantId AND c.IsDeleted = 0 AND c.Status = 4
       AND c.SubmittedAtUtc >= @FromUtc AND c.SubmittedAtUtc < @ToUtc) AS FinalizedTillCloses,
    (SELECT COUNT(*) FROM dbo.MobilePosTillCloseSubmissions c
     WHERE c.TenantId = @TenantId AND c.IsDeleted = 0 AND c.BankDepositBatchId IS NOT NULL
       AND c.SubmittedAtUtc >= @FromUtc AND c.SubmittedAtUtc < @ToUtc) AS DepositProposals,
    (SELECT COUNT(*) FROM @Findings) AS FindingCount;

IF EXISTS (SELECT 1 FROM @Findings)
    THROW 51000, 'Mobile POS accounting reconciliation found one or more control failures.', 1;

PRINT 'PASS Mobile POS accounting reconciliation';
