SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260916132000_DisposableDevelopmentCurrentModelBaseline')
    THROW 51100, 'GLF001: disposable-development baseline with final C8 schema is not applied.', 1;
IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AccountingEventProducerReceipts', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ProducerIntentGroups', N'U') IS NULL
    THROW 51101, 'GLF002: C6-C8 schema is incomplete.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.AccountAccountingBooks m
    JOIN dbo.Accounts a ON a.Id=m.AccountId
    JOIN dbo.AccountingBooks b ON b.Id=m.AccountingBookId
    LEFT JOIN dbo.AccountClassifications c ON c.Id=m.AccountClassificationId
    WHERE m.IsDeleted=0 AND m.IsEnabled=1 AND
      (a.Id IS NULL OR a.IsDeleted=1 OR b.Id IS NULL OR b.IsDeleted=1 OR b.IsActive=0 OR b.AllowsPosting=0 OR
       m.TenantId<>a.TenantId OR m.TenantId<>b.TenantId OR c.Id IS NULL OR c.IsDeleted=1 OR c.Status<>2 OR
       c.IsPostingClassification=0 OR c.TenantId<>m.TenantId OR c.AccountingBookId<>m.AccountingBookId OR
       c.CoreAccountType<>a.AccountType))
    THROW 51102, 'GLF003: enabled account/book mapping has invalid lineage.', 1;
IF EXISTS (
    SELECT TenantId,AccountingBookId,SystemRole FROM dbo.AccountClassifications
    WHERE IsDeleted=0 AND SystemRole IS NOT NULL AND SystemRole NOT IN (1,2)
    GROUP BY TenantId,AccountingBookId,SystemRole HAVING COUNT_BIG(*)>1)
    THROW 51103, 'GLF004: singleton classification role occurs more than once.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.AccountSegmentValues v
    JOIN dbo.Accounts a ON a.Id=v.AccountId
    JOIN dbo.AccountSegmentStructures s ON s.Id=v.SegmentStructureId
    LEFT JOIN dbo.SegmentLookupValues l ON l.Id=v.SegmentLookupValueId
    WHERE v.IsDeleted=0 AND (a.IsDeleted=1 OR s.IsDeleted=1 OR v.TenantId<>a.TenantId OR
      v.TenantId<>s.TenantId OR v.SegmentPosition<>s.SegmentPosition OR
      (v.SegmentLookupValueId IS NOT NULL AND (l.Id IS NULL OR l.IsDeleted=1 OR
       l.TenantId<>v.TenantId OR l.SegmentStructureId<>v.SegmentStructureId))))
    THROW 51104, 'GLF005: account segment value has invalid lineage.', 1;

-- Each canonical row is constructed as nvarchar(max) before SHA2_256 is evaluated. This avoids
-- SQL Server's 4,000-character Unicode CONCAT ceiling while retaining stable row ordering and every
-- material field. The transport receives a bounded stable key plus one fixed 64-character hash.
-- Audit-only timestamps/rowversions are excluded.
SELECT N'MIGRATION|' + CONVERT(nvarchar(150),MigrationId) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'MIGRATION|',MigrationId,N'|',ProductVersion)),2)
FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT N'ACCOUNT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNT|',Id,N'|',TenantId,N'|',AccountCode,N'|',AccountNumber,N'|',AccountName,N'|',AccountType,N'|',
  COALESCE(AccountCategory,N''),N'|',COALESCE(AccountSubCategory,N''),N'|',COALESCE(CONVERT(nvarchar(36),ParentAccountId),N''),N'|',IsSegmented,N'|',CurrencyCode,N'|',
  IsMultiCurrency,N'|',IsIFRSClassified,N'|',IsBaseClassified,N'|',IsLocalClassified,N'|',AllowDirectPosting,N'|',IsControlAccount,N'|',
  RequireDepartmentCode,N'|',RequireProjectCode,N'|',BudgetTrackingEnabled,N'|',Status,N'|',DebitBalance,N'|',CreditBalance,N'|',
  OpeningBalance,N'|',IsSystemAccount,N'|',IsDeleted)),2)
FROM dbo.Accounts ORDER BY TenantId,Id;
SELECT N'ACCOUNT_SEGMENT_VALUE|' + CONVERT(nvarchar(36),v.Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNT_SEGMENT_VALUE|',v.Id,N'|',v.TenantId,N'|',v.AccountId,N'|',v.SegmentStructureId,N'|',v.SegmentValue,N'|',
  COALESCE(CONVERT(nvarchar(36),v.SegmentLookupValueId),N''),N'|',COALESCE(v.SegmentValueDescription,N''),N'|',v.SegmentPosition,N'|',v.IsLocked,N'|',
  CONVERT(nvarchar(33),v.EffectiveDate,126),N'|',COALESCE(CONVERT(nvarchar(33),v.EndDate,126),N''),N'|',v.IsDeleted)),2)
FROM dbo.AccountSegmentValues v ORDER BY v.TenantId,v.AccountId,v.SegmentPosition,v.Id;
SELECT N'BOOK|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'BOOK|',Id,N'|',TenantId,N'|',Code,N'|',Name,N'|',Purpose,N'|',BookType,N'|',LifecycleStatus,N'|',
  COALESCE(FunctionalCurrencyCode,N''),N'|',COALESCE(CONVERT(nvarchar(33),EffectiveFromUtc,126),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),EffectiveToUtc,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),BaseAccountingBookId),N''),N'|',IsActive,N'|',IsDefault,N'|',
  AllowsPosting,N'|',IsSystemDefined,N'|',SortOrder,N'|',COALESCE(CONVERT(nvarchar(10),PendingLifecycleStatus),N''),N'|',IsDeleted)),2)
FROM dbo.AccountingBooks ORDER BY TenantId,Id;
SELECT N'CLASS|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'CLASS|',Id,N'|',TenantId,N'|',AccountingBookId,N'|',COALESCE(CONVERT(nvarchar(36),ParentClassificationId),N''),N'|',Code,N'|',Name,N'|',
  CoreAccountType,N'|',DefaultRevaluationTreatment,N'|',COALESCE(CONVERT(nvarchar(10),SystemRole),N''),N'|',IsPostingClassification,N'|',Status,N'|',DisplayOrder,N'|',IsDeleted)),2)
FROM dbo.AccountClassifications ORDER BY TenantId,AccountingBookId,Id;
SELECT N'MAP|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'MAP|',Id,N'|',TenantId,N'|',AccountId,N'|',AccountingBookId,N'|',COALESCE(CONVERT(nvarchar(36),AccountClassificationId),N''),N'|',
  IsEnabled,N'|',COALESCE(FinancialStatementLineItem,N''),N'|',IsDeleted)),2)
FROM dbo.AccountAccountingBooks ORDER BY TenantId,AccountId,AccountingBookId,Id;
SELECT N'SEGMENT_STRUCTURE|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'SEGMENT_STRUCTURE|',Id,N'|',TenantId,N'|',SegmentCode,N'|',SegmentPosition,N'|',SegmentLength,N'|',DataType,N'|',LifecycleStatus,N'|',IsDeleted)),2)
FROM dbo.AccountSegmentStructures ORDER BY TenantId,Id;
SELECT N'APPLICABILITY_POLICY|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'APPLICABILITY_POLICY|',Id,N'|',TenantId,N'|',PolicyCode,N'|',Version,N'|',COALESCE(CONVERT(nvarchar(36),SupersedesPolicyId),N''),N'|',Name,N'|',
  CONVERT(nvarchar(33),EffectiveFrom,126),N'|',COALESCE(CONVERT(nvarchar(33),EffectiveTo,126),N''),N'|',PolicyStatus,N'|',Reason,N'|',IsDeleted)),2)
FROM dbo.AccountingBookApplicabilityPolicies ORDER BY TenantId,Id;
SELECT N'APPLICABILITY_RULE|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'APPLICABILITY_RULE|',Id,N'|',TenantId,N'|',AccountingBookApplicabilityPolicyId,N'|',RuleCode,N'|',Priority,N'|',
  OriginatingModuleCode,N'|',SourceDocumentType,N'|',PostingAction,N'|',SortOrder,N'|',IsDeleted)),2)
FROM dbo.AccountingBookApplicabilityRules ORDER BY TenantId,AccountingBookApplicabilityPolicyId,Id;
SELECT N'APPLICABILITY_RULE_BOOK|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'APPLICABILITY_RULE_BOOK|',Id,N'|',TenantId,N'|',AccountingBookApplicabilityRuleId,N'|',AccountingBookId,N'|',
  SelectionOrder,N'|',AccountingBookCodeSnapshot,N'|',IsDeleted)),2)
FROM dbo.AccountingBookApplicabilityRuleBooks ORDER BY TenantId,AccountingBookApplicabilityRuleId,SelectionOrder,Id;
SELECT N'SELECTION_EVIDENCE|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'SELECTION_EVIDENCE|',Id,N'|',TenantId,N'|',COALESCE(CONVERT(nvarchar(36),AccountingBookApplicabilityPolicyId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),AccountingBookApplicabilityRuleId),N''),N'|',COALESCE(CONVERT(nvarchar(10),PolicyVersion),N''),N'|',CONVERT(nvarchar(33),EffectiveDate,126),N'|',
  OriginatingModuleCode,N'|',SourceDocumentType,N'|',PostingAction,N'|',IdempotencyKey,N'|',CalculationInputHash,N'|',SelectionFingerprint,N'|',IsDeleted)),2)
FROM dbo.AccountingBookSelectionEvidence ORDER BY TenantId,Id;
SELECT N'SELECTION_EVIDENCE_BOOK|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'SELECTION_EVIDENCE_BOOK|',Id,N'|',TenantId,N'|',AccountingBookSelectionEvidenceId,N'|',AccountingBookId,N'|',
  SelectionOrder,N'|',AccountingBookCodeSnapshot,N'|',AuthorityFingerprint,N'|',IsDeleted)),2)
FROM dbo.AccountingBookSelectionEvidenceBooks ORDER BY TenantId,AccountingBookSelectionEvidenceId,SelectionOrder,Id;
SELECT N'ACCOUNT_BALANCE|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNT_BALANCE|',Id,N'|',TenantId,N'|',AccountId,N'|',FiscalPeriodId,N'|',AccountingBookId,N'|',BookClassification,N'|',Currency,N'|',
  OpeningBalance,N'|',OpeningBalanceType,N'|',PeriodDebits,N'|',PeriodCredits,N'|',PeriodNetMovement,N'|',ClosingBalance,N'|',ClosingBalanceType,N'|',
  YearToDateDebits,N'|',YearToDateCredits,N'|',YearToDateNetMovement,N'|',COALESCE(SegmentString,N''),N'|',COALESCE(DepartmentSegment,N''),N'|',
  COALESCE(CostCenterSegment,N''),N'|',COALESCE(ProjectSegment,N''),N'|',COALESCE(LocationSegment,N''),N'|',
  COALESCE(CONVERT(nvarchar(50),ExchangeRate),N''),N'|',COALESCE(CONVERT(nvarchar(50),BaseCurrencyEquivalent),N''),N'|',
  COALESCE(CONVERT(nvarchar(50),UnrealizedGainLoss),N''),N'|',TransactionCount,N'|',COALESCE(CONVERT(nvarchar(33),LastTransactionDate,126),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),LastTransactionUserId),N''),N'|',CONVERT(nvarchar(33),LastUpdated,126),N'|',IsReconciled,N'|',
  COALESCE(CONVERT(nvarchar(33),LastReconciledDate,126),N''),N'|',COALESCE(CONVERT(nvarchar(50),ReconciliationDiscrepancy),N''),N'|',IsLocked,N'|',
  COALESCE(CONVERT(nvarchar(33),LockedDate,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),LockedByUserId),N''),N'|',HasActivity,N'|',IsZeroBalance,N'|',
  IsNegativeBalance,N'|',COALESCE(Notes,N''),N'|',IsDeleted)),2)
FROM dbo.AccountBalances ORDER BY TenantId,AccountingBookId,FiscalPeriodId,AccountId,Id;
SELECT N'ACCOUNT_CURRENCY_EXPOSURE|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNT_CURRENCY_EXPOSURE|',Id,N'|',TenantId,N'|',AccountId,N'|',AccountingBookId,N'|',AccountingBookCode,N'|',
  FunctionalCurrencyCode,N'|',TransactionCurrencyCode,N'|',SignedForeignBalance,N'|',SignedFunctionalBalance,N'|',TransactionCount,N'|',
  COALESCE(CONVERT(nvarchar(33),FirstTransactionDate,126),N''),N'|',COALESCE(CONVERT(nvarchar(33),LastTransactionDate,126),N''),N'|',
  CONVERT(nvarchar(33),LastRebuiltAt,126),N'|',SourceFingerprint,N'|',IsDeleted)),2)
FROM dbo.AccountCurrencyExposures ORDER BY TenantId,AccountingBookId,AccountId,TransactionCurrencyCode,Id;
SELECT N'ACCOUNTING_BOOK_PERIOD|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_BOOK_PERIOD|',Id,N'|',TenantId,N'|',AccountingBookId,N'|',FiscalPeriodId,N'|',PeriodStatus,N'|',
  COALESCE(CONVERT(nvarchar(10),PendingStatus),N''),N'|',COALESCE(PendingReason,N''),N'|',COALESCE(CONVERT(nvarchar(36),RequestedByUserId),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),RequestedAtUtc,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),WorkflowInstanceId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),DecidedByUserId),N''),N'|',COALESCE(CONVERT(nvarchar(33),DecidedAtUtc,126),N''),N'|',COALESCE(DecisionReason,N''),N'|',IsDeleted)),2)
FROM dbo.AccountingBookPeriods ORDER BY TenantId,AccountingBookId,FiscalPeriodId,Id;
SELECT N'ACCOUNTING_BOOK_INITIALIZATION|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_BOOK_INITIALIZATION|',Id,N'|',TenantId,N'|',AccountingBookId,N'|',Version,N'|',
  COALESCE(CONVERT(nvarchar(36),SupersedesInitializationId),N''),N'|',Mode,N'|',InitializationStatus,N'|',CONVERT(nvarchar(33),CutoffDate,126),N'|',
  CutoffFiscalPeriodId,N'|',COALESCE(CONVERT(nvarchar(36),SourceAccountingBookId),N''),N'|',IdempotencyKey,N'|',Reason,N'|',TotalDebits,N'|',TotalCredits,N'|',
  RequiredAccountCount,N'|',CoveredAccountCount,N'|',EvidenceFingerprint,N'|',ReconciliationFingerprint,N'|',PreparedByUserId,N'|',
  CONVERT(nvarchar(33),PreparedAtUtc,126),N'|',COALESCE(CONVERT(nvarchar(36),WorkflowInstanceId),N''),N'|',COALESCE(CONVERT(nvarchar(36),ApprovedByUserId),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),ApprovedAtUtc,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),RejectedByUserId),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),RejectedAtUtc,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),DecidedByUserId),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),DecidedAtUtc,126),N''),N'|',COALESCE(DecisionReason,N''),N'|',IsDeleted)),2)
FROM dbo.AccountingBookInitializations ORDER BY TenantId,AccountingBookId,Version,Id;
SELECT N'ACCOUNTING_BOOK_INITIALIZATION_LINE|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_BOOK_INITIALIZATION_LINE|',Id,N'|',TenantId,N'|',AccountingBookInitializationId,N'|',AccountId,N'|',CurrencyCode,N'|',
  OpeningDebit,N'|',OpeningCredit,N'|',BaseBookSignedBalance,N'|',OpeningAdjustment,N'|',IsDeleted)),2)
FROM dbo.AccountingBookInitializationLines ORDER BY TenantId,AccountingBookInitializationId,AccountId,CurrencyCode,Id;
SELECT N'JOURNAL_ENTRY|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'JOURNAL_ENTRY|',Id,N'|',TenantId,N'|',JournalEntryNumber,N'|',JournalType,N'|',CONVERT(nvarchar(33),EntryDate,126),N'|',Description,N'|',
  COALESCE(ReferenceNumber,N''),N'|',COALESCE(SourceModule,N''),N'|',COALESCE(OriginModuleCode,N''),N'|',COALESCE(CONVERT(nvarchar(36),SourceDocumentId),N''),N'|',
  COALESCE(SourceDocumentType,N''),N'|',TotalDebitAmount,N'|',TotalCreditAmount,N'|',BalanceDifference,N'|',IsBalanced,N'|',IsMultiCurrency,N'|',
  COALESCE(PrimaryCurrency,N''),N'|',BookClassification,N'|',AccountingBookId,N'|',FiscalPeriodId,N'|',COALESCE(CONVERT(nvarchar(33),PostingDate,126),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),PostedByUserId),N''),N'|',PostingStatus,N'|',RequiresApproval,N'|',COALESCE(ApprovalStatus,N''),N'|',
  COALESCE(ApprovalWorkflowId,N''),N'|',COALESCE(CONVERT(nvarchar(36),ApprovedByUserId),N''),N'|',COALESCE(CONVERT(nvarchar(33),ApprovedDate,126),N''),N'|',
  COALESCE(RejectionReason,N''),N'|',COALESCE(WithdrawalReason,N''),N'|',COALESCE(CONVERT(nvarchar(36),WithdrawnByUserId),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),WithdrawnDate,126),N''),N'|',IsReversed,N'|',COALESCE(CONVERT(nvarchar(33),ReversalDate,126),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),ReversalJournalEntryId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),OriginalJournalEntryId),N''),N'|',COALESCE(ReversalType,N''),N'|',COALESCE(ReversalReason,N''),N'|',IsRevaluationEntry,N'|',
  COALESCE(RevaluationBatchNumber,N''),N'|',COALESCE(RevaluationType,N''),N'|',IsAutoReversalEntry,N'|',IsRecurring,N'|',
  COALESCE(CONVERT(nvarchar(36),RecurringTemplateId),N''),N'|',COALESCE(RecurrenceFrequency,N''),N'|',COALESCE(CONVERT(nvarchar(33),NextRecurrenceDate,126),N''),N'|',
  IsImported,N'|',COALESCE(ImportBatchReference,N''),N'|',COALESCE(Notes,N''),N'|',COALESCE(EntryTag,N''),N'|',Priority,N'|',HasAttachments,N'|',AttachmentCount,N'|',IsDeleted)),2)
FROM dbo.JournalEntries ORDER BY TenantId,AccountingBookId,EntryDate,JournalEntryNumber,Id;
SELECT N'ACCOUNT_TRANSACTION|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNT_TRANSACTION|',Id,N'|',TenantId,N'|',AccountId,N'|',JournalEntryId,N'|',COALESCE(CONVERT(nvarchar(36),FinanceDimensionSetId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),FinanceDimensionSnapshotId),N''),N'|',CONVERT(nvarchar(33),TransactionDate,126),N'|',COALESCE(Description,N''),N'|',DebitAmount,N'|',CreditAmount,N'|',
  FunctionalCurrencyCode,N'|',COALESCE(TransactionCurrency,N''),N'|',COALESCE(CONVERT(nvarchar(50),TransactionDebitAmount),N''),N'|',COALESCE(CONVERT(nvarchar(50),TransactionCreditAmount),N''),N'|',
  COALESCE(CONVERT(nvarchar(50),ForeignCurrencyAmount),N''),N'|',COALESCE(CONVERT(nvarchar(50),ExchangeRate),N''),N'|',COALESCE(CONVERT(nvarchar(36),ExchangeRateId),N''),N'|',
  COALESCE(ExchangeRateSource,N''),N'|',COALESCE(CONVERT(nvarchar(33),ExchangeRateDate,126),N''),N'|',COALESCE(SourceModule,N''),N'|',
  COALESCE(CONVERT(nvarchar(36),SourceDocumentId),N''),N'|',COALESCE(CONVERT(nvarchar(36),SourceDocumentLineId),N''),N'|',COALESCE(SourceDocumentType,N''),N'|',
  COALESCE(SourceReferenceNumber,N''),N'|',BookClassification,N'|',AccountingBookId,N'|',FiscalPeriodId,N'|',PostingStatus,N'|',IsReversed,N'|',
  COALESCE(CONVERT(nvarchar(33),PostedDate,126),N''),N'|',COALESCE(CONVERT(nvarchar(33),ReversalDate,126),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),ReversalTransactionId),N''),N'|',COALESCE(CONVERT(nvarchar(36),OriginalTransactionId),N''),N'|',
  COALESCE(ReversalType,N''),N'|',COALESCE(ReversalReason,N''),N'|',COALESCE(SegmentString,N''),N'|',IsRevaluationEntry,N'|',
  COALESCE(RevaluationBatchNumber,N''),N'|',COALESCE(RevaluationType,N''),N'|',LineNumber,N'|',COALESCE(Notes,N''),N'|',COALESCE(TransactionTag,N''),N'|',IsDeleted)),2)
FROM dbo.AccountTransactions ORDER BY TenantId,AccountingBookId,JournalEntryId,LineNumber,Id;
SELECT N'FINANCE_POSTING_EVENT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'FINANCE_POSTING_EVENT|',Id,N'|',TenantId,N'|',SourceModule,N'|',COALESCE(OriginModuleCode,N''),N'|',SourceDocumentType,N'|',SourceDocumentId,N'|',
  PostingAction,N'|',COALESCE(SourceDocumentReference,N''),N'|',COALESCE(IdempotencyKey,N''),N'|',COALESCE(RequestFingerprintVersion,N''),N'|',
  COALESCE(RequestFingerprint,N''),N'|',COALESCE(CONVERT(nvarchar(36),JournalEntryId),N''),N'|',PostingStatus,N'|',CONVERT(nvarchar(33),PostingDate,126),N'|',
  CONVERT(nvarchar(33),RequestedAt,126),N'|',COALESCE(CONVERT(nvarchar(33),PostedAt,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),RequestedByUserId),N''),N'|',
  TotalDebitAmount,N'|',TotalCreditAmount,N'|',FunctionalCurrencyCode,N'|',HasForeignCurrencyLines,N'|',COALESCE(PrimaryTransactionCurrencyCode,N''),N'|',
  COALESCE(CONVERT(nvarchar(36),PrimaryExchangeRateId),N''),N'|',COALESCE(CONVERT(nvarchar(50),PrimaryExchangeRate),N''),N'|',COALESCE(CONVERT(nvarchar(33),PrimaryExchangeRateDate,126),N''),N'|',
  BookClassification,N'|',AccountingBookId,N'|',COALESCE(ErrorMessage,N''),N'|',IsDeleted)),2)
FROM dbo.FinancePostingEvents ORDER BY TenantId,AccountingBookId,PostingDate,SourceDocumentId,PostingAction,Id;
SELECT N'ACCOUNTING_EVENT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_EVENT|',Id,N'|',TenantId,N'|',OriginatingModuleCode,N'|',SourceDocumentType,N'|',SourceDocumentId,N'|',
  PostingAction,N'|',IdempotencyKey,N'|',EventKind,N'|',Version,N'|',RootAccountingEventId,N'|',COALESCE(CONVERT(nvarchar(36),SupersedesAccountingEventId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),CorrectsAccountingEventId),N''),N'|',COALESCE(CONVERT(nvarchar(36),ReversesAccountingEventId),N''),N'|',COALESCE(CONVERT(nvarchar(36),AccountingBookSelectionEvidenceId),N''),N'|',
  SelectionFingerprint,N'|',RequestFingerprint,N'|',Status,N'|',ProducerDecisionStatus,N'|',COALESCE(ProducerParticipantIdentity,N''),N'|',
  COALESCE(ProducerIntentSnapshotHash,N''),N'|',IsDeleted)),2)
FROM dbo.AccountingEvents ORDER BY TenantId,Id;
SELECT N'ACCOUNTING_EVENT_POSTING|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_EVENT_POSTING|',Id,N'|',TenantId,N'|',AccountingEventId,N'|',EventVersion,N'|',AccountingBookId,N'|',SelectionOrder,N'|',
  AccountingBookCodeSnapshot,N'|',AuthorityFingerprint,N'|',Status,N'|',COALESCE(CONVERT(nvarchar(36),FinancePostingEventId),N''),N'|',COALESCE(CONVERT(nvarchar(36),JournalEntryId),N''),N'|',IsDeleted)),2)
FROM dbo.AccountingEventPostings ORDER BY TenantId,AccountingEventId,SelectionOrder,Id;
SELECT N'ACCOUNTING_EVENT_RECEIPT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_EVENT_RECEIPT|',Id,N'|',TenantId,N'|',AccountingEventId,N'|',ParticipantCode,N'|',OwnerEntityType,N'|',OwnerEntityId,N'|',
  OwnerAction,N'|',EffectFingerprint,N'|',RequestFingerprint,N'|',IsDeleted)),2)
FROM dbo.AccountingEventProducerReceipts ORDER BY TenantId,AccountingEventId,Id;
SELECT N'ACCOUNTING_EVENT_ATTEMPT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'ACCOUNTING_EVENT_ATTEMPT|',Id,N'|',TenantId,N'|',AccountingEventId,N'|',AttemptNumber,N'|',RequestFingerprint,N'|',Status,N'|',IsDeleted)),2)
FROM dbo.AccountingEventAttempts ORDER BY TenantId,AccountingEventId,AttemptNumber,Id;
SELECT N'PRODUCER_INTENT_GROUP|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'PRODUCER_INTENT_GROUP|',Id,N'|',TenantId,N'|',IdempotencyKey,N'|',GroupKind,N'|',Version,N'|',RootProducerIntentGroupId,N'|',
  COALESCE(CONVERT(nvarchar(36),SupersedesProducerIntentGroupId),N''),N'|',COALESCE(CONVERT(nvarchar(36),CorrectsProducerIntentGroupId),N''),N'|',COALESCE(CONVERT(nvarchar(36),ReversesProducerIntentGroupId),N''),N'|',
  Status,N'|',MemberCount,N'|',ParticipantCode,N'|',OwnerEntityType,N'|',OwnerEntityId,N'|',OwnerAction,N'|',ExpectedOwnerEffectFingerprint,N'|',
  RequestSnapshotHash,N'|',GroupFingerprint,N'|',IsDeleted)),2)
FROM dbo.ProducerIntentGroups ORDER BY TenantId,Id;
SELECT N'PRODUCER_INTENT_GROUP_MEMBER|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'PRODUCER_INTENT_GROUP_MEMBER|',Id,N'|',TenantId,N'|',ProducerIntentGroupId,N'|',AccountingEventId,N'|',MemberOrder,N'|',MemberFingerprint,N'|',IsDeleted)),2)
FROM dbo.ProducerIntentGroupMembers ORDER BY TenantId,ProducerIntentGroupId,MemberOrder,Id;
SELECT N'PRODUCER_INTENT_GROUP_RECEIPT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'PRODUCER_INTENT_GROUP_RECEIPT|',Id,N'|',TenantId,N'|',ProducerIntentGroupId,N'|',ParticipantCode,N'|',OwnerEntityType,N'|',OwnerEntityId,N'|',
  OwnerAction,N'|',EffectFingerprint,N'|',GroupFingerprint,N'|',IsDeleted)),2)
FROM dbo.ProducerIntentGroupReceipts ORDER BY TenantId,ProducerIntentGroupId,Id;
SELECT N'PRODUCER_INTENT_GROUP_ATTEMPT|' + CONVERT(nvarchar(36),Id) + N'|' + CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CAST(N'' AS nvarchar(max)),N'PRODUCER_INTENT_GROUP_ATTEMPT|',Id,N'|',TenantId,N'|',ProducerIntentGroupId,N'|',AttemptNumber,N'|',GroupFingerprint,N'|',Status,N'|',
  COALESCE(CONVERT(nvarchar(10),FailedMemberOrder),N''),N'|',COALESCE(CONVERT(nvarchar(36),FailedAccountingEventId),N''),N'|',IsDeleted)),2)
FROM dbo.ProducerIntentGroupAttempts ORDER BY TenantId,ProducerIntentGroupId,AttemptNumber,Id;
SELECT N'CONTROL_COUNTS|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.JournalEntries WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountTransactions WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.FinancePostingEvents WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountingEvents WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountingEventProducerReceipts WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.ProducerIntentGroups WHERE IsDeleted=0));
