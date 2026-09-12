SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260908120000_AddProducerIntentGroupsC8')
    THROW 51100, 'GLF001: final C8 migration is not applied.', 1;
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

-- Canonical content, including stable IDs and authority/state fields, makes same-count mutation
-- visible. Audit-only timestamps/rowversions are excluded; rows and fields have fixed ordering.
SELECT CONCAT(N'MIGRATION|',MigrationId,N'|',ProductVersion)
FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT CONCAT(N'ACCOUNT|',Id,N'|',TenantId,N'|',AccountCode,N'|',AccountNumber,N'|',AccountName,N'|',AccountType,N'|',
  COALESCE(AccountCategory,N''),N'|',COALESCE(AccountSubCategory,N''),N'|',COALESCE(CONVERT(nvarchar(36),ParentAccountId),N''),N'|',IsSegmented,N'|',CurrencyCode,N'|',
  IsMultiCurrency,N'|',IsIFRSClassified,N'|',IsBaseClassified,N'|',IsLocalClassified,N'|',AllowDirectPosting,N'|',IsControlAccount,N'|',
  RequireDepartmentCode,N'|',RequireProjectCode,N'|',BudgetTrackingEnabled,N'|',Status,N'|',Balance,N'|',DebitBalance,N'|',CreditBalance,N'|',
  OpeningBalance,N'|',IsSystemAccount,N'|',IsDeleted)
FROM dbo.Accounts ORDER BY TenantId,Id;
SELECT CONCAT(N'ACCOUNT_SEGMENT_VALUE|',v.Id,N'|',v.TenantId,N'|',v.AccountId,N'|',v.SegmentStructureId,N'|',v.SegmentValue,N'|',
  COALESCE(CONVERT(nvarchar(36),v.SegmentLookupValueId),N''),N'|',COALESCE(v.SegmentValueDescription,N''),N'|',v.SegmentPosition,N'|',v.IsLocked,N'|',
  CONVERT(nvarchar(33),v.EffectiveDate,126),N'|',COALESCE(CONVERT(nvarchar(33),v.EndDate,126),N''),N'|',v.IsDeleted)
FROM dbo.AccountSegmentValues v ORDER BY v.TenantId,v.AccountId,v.SegmentPosition,v.Id;
SELECT CONCAT(N'BOOK|',Id,N'|',TenantId,N'|',Code,N'|',Name,N'|',Purpose,N'|',BookType,N'|',LifecycleStatus,N'|',
  COALESCE(FunctionalCurrencyCode,N''),N'|',COALESCE(CONVERT(nvarchar(33),EffectiveFromUtc,126),N''),N'|',
  COALESCE(CONVERT(nvarchar(33),EffectiveToUtc,126),N''),N'|',COALESCE(CONVERT(nvarchar(36),BaseAccountingBookId),N''),N'|',IsActive,N'|',IsDefault,N'|',
  AllowsPosting,N'|',IsSystemDefined,N'|',SortOrder,N'|',COALESCE(CONVERT(nvarchar(10),PendingLifecycleStatus),N''),N'|',IsDeleted)
FROM dbo.AccountingBooks ORDER BY TenantId,Id;
SELECT CONCAT(N'CLASS|',Id,N'|',TenantId,N'|',AccountingBookId,N'|',COALESCE(CONVERT(nvarchar(36),ParentClassificationId),N''),N'|',Code,N'|',Name,N'|',
  CoreAccountType,N'|',DefaultRevaluationTreatment,N'|',COALESCE(CONVERT(nvarchar(10),SystemRole),N''),N'|',IsPostingClassification,N'|',Status,N'|',DisplayOrder,N'|',IsDeleted)
FROM dbo.AccountClassifications ORDER BY TenantId,AccountingBookId,Id;
SELECT CONCAT(N'MAP|',Id,N'|',TenantId,N'|',AccountId,N'|',AccountingBookId,N'|',COALESCE(CONVERT(nvarchar(36),AccountClassificationId),N''),N'|',
  IsEnabled,N'|',COALESCE(FinancialStatementLineItem,N''),N'|',IsDeleted)
FROM dbo.AccountAccountingBooks ORDER BY TenantId,AccountId,AccountingBookId,Id;
SELECT CONCAT(N'SEGMENT_STRUCTURE|',Id,N'|',TenantId,N'|',SegmentCode,N'|',SegmentPosition,N'|',SegmentLength,N'|',DataType,N'|',LifecycleStatus,N'|',IsDeleted)
FROM dbo.AccountSegmentStructures ORDER BY TenantId,Id;
SELECT CONCAT(N'APPLICABILITY_POLICY|',Id,N'|',TenantId,N'|',PolicyCode,N'|',Version,N'|',COALESCE(CONVERT(nvarchar(36),SupersedesPolicyId),N''),N'|',Name,N'|',
  CONVERT(nvarchar(33),EffectiveFrom,126),N'|',COALESCE(CONVERT(nvarchar(33),EffectiveTo,126),N''),N'|',PolicyStatus,N'|',Reason,N'|',IsDeleted)
FROM dbo.AccountingBookApplicabilityPolicies ORDER BY TenantId,Id;
SELECT CONCAT(N'APPLICABILITY_RULE|',Id,N'|',TenantId,N'|',AccountingBookApplicabilityPolicyId,N'|',RuleCode,N'|',Priority,N'|',
  OriginatingModuleCode,N'|',SourceDocumentType,N'|',PostingAction,N'|',SortOrder,N'|',IsDeleted)
FROM dbo.AccountingBookApplicabilityRules ORDER BY TenantId,AccountingBookApplicabilityPolicyId,Id;
SELECT CONCAT(N'APPLICABILITY_RULE_BOOK|',Id,N'|',TenantId,N'|',AccountingBookApplicabilityRuleId,N'|',AccountingBookId,N'|',
  SelectionOrder,N'|',AccountingBookCodeSnapshot,N'|',IsDeleted)
FROM dbo.AccountingBookApplicabilityRuleBooks ORDER BY TenantId,AccountingBookApplicabilityRuleId,SelectionOrder,Id;
SELECT CONCAT(N'SELECTION_EVIDENCE|',Id,N'|',TenantId,N'|',COALESCE(CONVERT(nvarchar(36),AccountingBookApplicabilityPolicyId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),AccountingBookApplicabilityRuleId),N''),N'|',COALESCE(CONVERT(nvarchar(10),PolicyVersion),N''),N'|',CONVERT(nvarchar(33),EffectiveDate,126),N'|',
  OriginatingModuleCode,N'|',SourceDocumentType,N'|',PostingAction,N'|',IdempotencyKey,N'|',CalculationInputHash,N'|',SelectionFingerprint,N'|',IsDeleted)
FROM dbo.AccountingBookSelectionEvidence ORDER BY TenantId,Id;
SELECT CONCAT(N'SELECTION_EVIDENCE_BOOK|',Id,N'|',TenantId,N'|',AccountingBookSelectionEvidenceId,N'|',AccountingBookId,N'|',
  SelectionOrder,N'|',AccountingBookCodeSnapshot,N'|',AuthorityFingerprint,N'|',IsDeleted)
FROM dbo.AccountingBookSelectionEvidenceBooks ORDER BY TenantId,AccountingBookSelectionEvidenceId,SelectionOrder,Id;
SELECT CONCAT(N'ACCOUNTING_EVENT|',Id,N'|',TenantId,N'|',OriginatingModuleCode,N'|',SourceDocumentType,N'|',SourceDocumentId,N'|',
  PostingAction,N'|',IdempotencyKey,N'|',EventKind,N'|',Version,N'|',RootAccountingEventId,N'|',COALESCE(CONVERT(nvarchar(36),SupersedesAccountingEventId),N''),N'|',
  COALESCE(CONVERT(nvarchar(36),CorrectsAccountingEventId),N''),N'|',COALESCE(CONVERT(nvarchar(36),ReversesAccountingEventId),N''),N'|',COALESCE(CONVERT(nvarchar(36),AccountingBookSelectionEvidenceId),N''),N'|',
  SelectionFingerprint,N'|',RequestFingerprint,N'|',Status,N'|',ProducerDecisionStatus,N'|',COALESCE(ProducerParticipantIdentity,N''),N'|',
  COALESCE(ProducerIntentSnapshotHash,N''),N'|',IsDeleted)
FROM dbo.AccountingEvents ORDER BY TenantId,Id;
SELECT CONCAT(N'ACCOUNTING_EVENT_POSTING|',Id,N'|',TenantId,N'|',AccountingEventId,N'|',EventVersion,N'|',AccountingBookId,N'|',SelectionOrder,N'|',
  AccountingBookCodeSnapshot,N'|',AuthorityFingerprint,N'|',Status,N'|',COALESCE(CONVERT(nvarchar(36),FinancePostingEventId),N''),N'|',COALESCE(CONVERT(nvarchar(36),JournalEntryId),N''),N'|',IsDeleted)
FROM dbo.AccountingEventPostings ORDER BY TenantId,AccountingEventId,SelectionOrder,Id;
SELECT CONCAT(N'ACCOUNTING_EVENT_RECEIPT|',Id,N'|',TenantId,N'|',AccountingEventId,N'|',ParticipantCode,N'|',OwnerEntityType,N'|',OwnerEntityId,N'|',
  OwnerAction,N'|',EffectFingerprint,N'|',RequestFingerprint,N'|',IsDeleted)
FROM dbo.AccountingEventProducerReceipts ORDER BY TenantId,AccountingEventId,Id;
SELECT CONCAT(N'ACCOUNTING_EVENT_ATTEMPT|',Id,N'|',TenantId,N'|',AccountingEventId,N'|',AttemptNumber,N'|',RequestFingerprint,N'|',Status,N'|',IsDeleted)
FROM dbo.AccountingEventAttempts ORDER BY TenantId,AccountingEventId,AttemptNumber,Id;
SELECT CONCAT(N'PRODUCER_INTENT_GROUP|',Id,N'|',TenantId,N'|',IdempotencyKey,N'|',GroupKind,N'|',Version,N'|',RootProducerIntentGroupId,N'|',
  COALESCE(CONVERT(nvarchar(36),SupersedesProducerIntentGroupId),N''),N'|',COALESCE(CONVERT(nvarchar(36),CorrectsProducerIntentGroupId),N''),N'|',COALESCE(CONVERT(nvarchar(36),ReversesProducerIntentGroupId),N''),N'|',
  Status,N'|',MemberCount,N'|',ParticipantCode,N'|',OwnerEntityType,N'|',OwnerEntityId,N'|',OwnerAction,N'|',ExpectedOwnerEffectFingerprint,N'|',
  RequestSnapshotHash,N'|',GroupFingerprint,N'|',IsDeleted)
FROM dbo.ProducerIntentGroups ORDER BY TenantId,Id;
SELECT CONCAT(N'PRODUCER_INTENT_GROUP_MEMBER|',Id,N'|',TenantId,N'|',ProducerIntentGroupId,N'|',AccountingEventId,N'|',MemberOrder,N'|',MemberFingerprint,N'|',IsDeleted)
FROM dbo.ProducerIntentGroupMembers ORDER BY TenantId,ProducerIntentGroupId,MemberOrder,Id;
SELECT CONCAT(N'PRODUCER_INTENT_GROUP_RECEIPT|',Id,N'|',TenantId,N'|',ProducerIntentGroupId,N'|',ParticipantCode,N'|',OwnerEntityType,N'|',OwnerEntityId,N'|',
  OwnerAction,N'|',EffectFingerprint,N'|',GroupFingerprint,N'|',IsDeleted)
FROM dbo.ProducerIntentGroupReceipts ORDER BY TenantId,ProducerIntentGroupId,Id;
SELECT CONCAT(N'PRODUCER_INTENT_GROUP_ATTEMPT|',Id,N'|',TenantId,N'|',ProducerIntentGroupId,N'|',AttemptNumber,N'|',GroupFingerprint,N'|',Status,N'|',
  COALESCE(CONVERT(nvarchar(10),FailedMemberOrder),N''),N'|',COALESCE(CONVERT(nvarchar(36),FailedAccountingEventId),N''),N'|',IsDeleted)
FROM dbo.ProducerIntentGroupAttempts ORDER BY TenantId,ProducerIntentGroupId,AttemptNumber,Id;
SELECT N'CONTROL_COUNTS|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.JournalEntries WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountTransactions WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.FinancePostingEvents WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountingEvents WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountingEventProducerReceipts WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.ProducerIntentGroups WHERE IsDeleted=0));
